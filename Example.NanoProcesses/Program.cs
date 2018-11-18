using KC.NanoProcesses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Example.NanoProcesses
{
    class Program
    {
        static void Main(string[] args)
        {
            var manager = new NanoProcessManager(new Logger());
            manager.PrintRunningProcessesToConsoleIfDebug = true;

            //This will run until the user hits Q or Escape. (If Environment is interactive.)
            //All Processes marked with the NanoDI attribute will be automatically created
            //and passed their dependencies.
            manager.Run(startUp: async (util) => {
                //Run special startup code if needed, and manually add processes or dependencies:
                Console.WriteLine("This runs before processes are automatically instantiated.");
                await Task.FromResult(0);
            }).Wait();
        }
    }

    /// <summary>
    /// This will generate a new number once per second.
    /// </summary>
    [NanoDI]
    class GetWork : NanoProcess {

        private SaveWork saveWork;
        public GetWork(SaveWork _saveWork) {
            //Automatically created and passed arguments using reflection.
            this.saveWork = _saveWork;
        }

        public override string ProcessName => nameof(GetWork);
        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 1);

        protected async override Task OnInit(NpUtil util) {
            await Task.FromResult(0);
        }

        protected async override Task OnDispose(NpUtil util) {
            await Task.FromResult(0);
        }

        private Random r = new Random();
        protected async override Task OnRun(NpUtil util) {
            var number = r.Next();
            Console.WriteLine(number);
            saveWork.Enqueue(number);
            await Task.FromResult(0);
        }
    }

    /// <summary>
    /// This will write numbers to disk every 15 seconds.
    /// It will be automatically created using reflection,
    /// and passed to anything which needs a reference to it.
    /// </summary>
    [NanoDI]
    class SaveWork : NanoQueue<int> {

        public override string ProcessName => nameof(SaveWork);
        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 15);

        protected async override Task OnInit(NpUtil util) {
            await Task.FromResult(0);
        }

        protected async override Task OnDispose(NpUtil util) {
            await Task.FromResult(0);
        }

        protected async override Task<Queue<int>> OnRun(NpUtil util, Queue<int> items) {
            var sb = new StringBuilder();
            while (items.Count > 0) {
                sb.AppendLine(items.Dequeue().ToString());
            }
            await File.WriteAllTextAsync("./numbers.txt", sb.ToString());
            return items;
        }
    }

    /// <summary>
    /// The simplest version of a logging class.
    /// </summary>
    class Logger : INanoProcessLogger {
        public void Error(string context, string location, string message) {
            Console.WriteLine($"Context: {(context ?? "null")}, Location: {(location ?? "null")}, Message: {message ?? "null"}");
        }

        public void Error(string context, string location, Exception ex) {
            this.Error(context, location, ex?.ToString());
        }

        public void RealTime(string context, string location, string message) {
            this.Error(context, location, message);
        }

        public void RealTime(string context, string location, Exception ex) {
            this.Error(context, location, ex);
        }
    }
}
