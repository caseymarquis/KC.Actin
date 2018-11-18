# NanoProcesses

This is a library for creating a .NET application with a 'nanoprocess' architecture.
This architecture promotes separation of concerns and clear dependency specification
within a monolithic application, and allows specific pieces of the application to be
identified and easily converted to separate services when it is determined that the
performance benefits are worth the increased complexity of creating a 'microservice'.

This architecture aims to ease a few problems:
* Monoliths are easy to debug, but they fail to scale well horizontally.
* Microservices can be painful to work with, and are often used poorly or prematurely (ie distributed monoliths or nanoservices), but scale extremely well.
* Microservices are promoted as forcing a separation of concerns, but the price of doing so is often not proportional to the benefit.
* Moving a monolith over to microservices can fail spectacularly in a variety of ways.

### Installing

* .Net Framework: Install-Package KC.NanoProcesses
* .Net Core: dotnet add package KC.NanoProcesses

## Getting Started

The below code creates two processes. One generates random numbers, and the other writes them to disk.
In a real world application, one process might be pulling data from an external system, and the
other could be cleaning it and writing it to a database.

While processes can be created manually, they are typically created automatically with reflection.
This is similar to how controllers are automagically created in ASP.NET.

```C#
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
```

Currently, there is no support for automagically instantiating an interface.
While it wouldn't be too difficult to extend the startup Util class to allow
this via something like util.SetInterface<IDoSomething, DoSomething>(), I think interfaces
are overused in the 1 interface per class anti-pattern, so I'm not in a rush to do this.
Processes which require interfaces in their constructors can instantiate themselves in
the manual start up code.