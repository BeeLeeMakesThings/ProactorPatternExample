using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ProactorApp
{
    /// <summary>
    /// Runs a background thread that will dispatch events from queue for processing
    /// </summary>
    public class ProactorQueue
    {
        /// <summary>
        /// Number of worker threads
        /// </summary>
        public int WorkerCount { get; private set; }

        private Thread[] workerThreads;
        private Thread dispatcher;
        private readonly AutoResetEvent dispatcherSignal;

        private bool isRunning = true;

        private Queue<ProactorQueueData> taskQueue;
        private Queue<ProactorQueueData> finishedQueue;

        // A lock to synchronize access to the task queue
        private readonly object taskQueueLock = new object();
        private readonly object finishedQueueLock = new object();

        /// <summary>
        /// Creates this instance with the given number of worker threads
        /// </summary>
        /// <param name="workerCount"></param>
        public ProactorQueue(
            int workerCount = 3
            )
        {
            WorkerCount = workerCount;

            taskQueue = new Queue<ProactorQueueData>();
            finishedQueue = new Queue<ProactorQueueData>();

            workerThreads = new Thread[workerCount];
            dispatcher = new Thread(OnDispatcherStart)
            {
                Name = "DISPATCHER"
            };

            dispatcherSignal = new AutoResetEvent(false);
        }

        /// <summary>
        /// Starts the dispatcher thread
        /// </summary>
        public void Start()
        {
            if (dispatcher != null) dispatcher.Start();
        }

        /// <summary>
        /// Stops the dispatcher
        /// </summary>
        public void Stop()
        {
            if(dispatcher!=null && isRunning)
            {
                isRunning = false;
                dispatcherSignal.Set();
                dispatcher.Join();
                dispatcher = null;
            }
        }

        /// <summary>
        /// Enqueues a function to the dispatcher queue
        /// </summary>
        public ProactorTask Enqueue(Func<object> resultFunction, Action<object> finishCallback = null)
        {
            ProactorQueueData data = new ProactorQueueData(resultFunction, finishCallback);
            Log($"Queueing a new task ({data.QueuedCode})");

            lock (taskQueueLock)
                taskQueue.Enqueue(data);
            
            // we can signal to the dispatcher thread to stop waiting for new tasks
            // and resume
            dispatcherSignal.Set();
            
            return data.Task;
        }

        /// <summary>
        /// Wraps around a return-less function
        /// </summary>
        public ProactorTask Enqueue(Action voidFunction, Action finishCallback = null)
        {
            return Enqueue(
                () => { voidFunction(); return null; },
                _ => finishCallback?.Invoke()
                );
        }

        /// <summary>
        /// The dispatcher logic
        /// </summary>
        private void OnDispatcherStart()
        {
            while(isRunning)
            {
                ProactorQueueData task = null;
                bool hasTask = false;

                // do we have any completion task to finalise?
                SettleCompletedTasks();

                // do we have free threads?
                int threadIndex = GetNextAvailableThread();
                if(threadIndex < 0)
                {
                    // nope, let's wait until we have one
                    dispatcherSignal.WaitOne();
                    continue;
                }

                lock (taskQueueLock)
                    hasTask = taskQueue.TryDequeue(out task);

                if(!hasTask)
                {
                    // we can sleep the thread until a new task comes up
                    // but we can check again after 5 minutes in case the signal
                    // was never called
                    dispatcherSignal.WaitOne(TimeSpan.FromMinutes(5));
                    continue;
                }

                if (task == null)
                    continue; // sanity check

                // we can now dispatch this task over to the thread
                Log($"Dispatching task ({task.QueuedCode}) to thread {threadIndex}");
                workerThreads[threadIndex] = new Thread(WorkerThreadMethod)
                {
                    Name = $"WORKER-{threadIndex}"
                };

                task.ThreadIndex = threadIndex; // to keep track of the thread serving this task
                workerThreads[threadIndex].Start(task);
            }

            // ensure all threads are done
            for(int i=0;i<WorkerCount;i++)
                workerThreads[i]?.Join();
            SettleCompletedTasks();
        }

        private void WorkerThreadMethod(object data)
        {
            Log($"Worker thread start");

            if (data == null && !(data is ProactorQueueData))
                throw new ArgumentException($"{nameof(data)} must be of {nameof(ProactorQueueData)} type");

            ProactorQueueData task = (ProactorQueueData)data;
            object result = task.Operation();

            task.Task.Finished = true;
            task.Task.Result = result;

            // we have something to run on completion, on the dispatcher thread, so we queue it
            // and signal the dispatcher
            lock (finishedQueueLock)
                finishedQueue.Enqueue(task);

            dispatcherSignal.Set();
        }

        private void SettleCompletedTasks()
        {
            lock (finishedQueueLock)
            {
                while (finishedQueue.Count > 0)
                {
                    ProactorQueueData finishedTask = finishedQueue.Dequeue();
                    Log($"Finishing task ({finishedTask.QueuedCode})");

                    // call the callback if not null
                    finishedTask.FinishCallback?.Invoke(finishedTask.Task.Result);

                    // free up the corresponding thread
                    int tid = finishedTask.ThreadIndex;
                    workerThreads[tid].Join();
                    workerThreads[tid] = null;

                    Log($"Freed thread {tid}");
                }
            }
        }

        private int GetNextAvailableThread()
        {
            for(int i=0;i<WorkerCount;i++)
                if (workerThreads[i] == null) return i;
            return -1;
        }

        private void Log(string msg)
        {
            Program.Log(msg);
        }
    }
}
