using System;
using System.Collections.Generic;
using System.Text;

namespace ProactorApp
{
    /// <summary>
    /// Internal entry in the proactor queue
    /// </summary>
    public class ProactorQueueData
    {
        private static int _nextQueueCode = 0;

        public ProactorTask Task { get; }
        public Func<object> Operation { get; }
        public Action<object> FinishCallback { get; }

        public int ThreadIndex { get; set; } = -1;

        /// <summary>
        /// The unique integer representing the time at which this data was created
        /// </summary>
        public int QueuedCode { get; }

        public ProactorQueueData(
            Func<object> operation,
            Action<object> finishCallback
            )
        {
            Task = new ProactorTask();
            Operation = operation;
            FinishCallback = finishCallback;
            QueuedCode = _nextQueueCode++;
        }
    }
}
