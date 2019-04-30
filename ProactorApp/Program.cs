using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProactorApp
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().Run();
        }

        private readonly ProactorQueue queue;
        private readonly Random random;

        public Program()
        {
            Thread.CurrentThread.Name = "MAIN";

            random = new Random();
            queue = new ProactorQueue();
            queue.Start();
        }

        private void Run()
        {
            bool running = true;
            DisplayHelp();

            while (running)
            {
                Console.Write(">> ");

                string input = Console.ReadLine();
                switch (input.Trim().ToLower())
                {
                    case "long":
                        queue.Enqueue(() => { Task.Delay(10000).Wait(); });
                        Log("10 seconds process queued");
                        break;
                    case "short":
                        queue.Enqueue(() => { Task.Delay(5000).Wait(); });
                        Log("5 seconds process queued");
                        break;
                    case "longres":
                        queue.Enqueue(
                            () => { Task.Delay(10000).Wait(); return 10; },
                            OnResult
                            );
                        Log("10 seconds process with result queued");
                        break;
                    case "shortres":
                        queue.Enqueue(() => { Task.Delay(5000).Wait(); return 5; },
                            OnResult
                            );
                        Log("5 seconds process with result queued");
                        break;
                    case "random":
                        for (int i = 0; i < 20; i++)
                        {
                            int taskTime = random.Next(1000, 10000);
                            queue.Enqueue(
                                () => { Task.Delay(taskTime).Wait(); },
                                () => { Log($"Task with duration {taskTime} ms finished"); }
                            );

                        }
                        Log("Queued 20 tasks");
                        break;
                    case "quit":
                        Log("Stopping dispatcher. Hang on...");
                        queue.Stop();
                        running = false;
                        break;
                    default:
                        DisplayHelp();
                        break;
                }
            }
        }

        private void DisplayHelp()
        {
            Console.WriteLine("Proactor Pattern Example");
            Console.WriteLine("");
            Console.WriteLine("Use the following commands:");
            Console.WriteLine("  help      : Display this help text");
            Console.WriteLine("  short     : Runs a 5-second process");
            Console.WriteLine("  shortres  : Runs a 5-second process that returns a result");
            Console.WriteLine("  long      : Runs a 10-second process");
            Console.WriteLine("  longres   : Runs a 10-second process that returns a result");
            Console.WriteLine("  random    : Queues 20 processes of varying durations");
            Console.WriteLine("  quit      : Ends the program");
            Console.WriteLine();
        }

        private void OnResult(object obj)
        {
            Log($"RESULT: {obj.ToString()}");
        }

        public static void Log(string msg)
        {
            string thrName = Thread.CurrentThread.Name;
            Console.WriteLine($"-- [THR: {thrName}] {msg}");
        }
    }
}
