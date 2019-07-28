# Actin

Actin is a single process 'Actor Framework' for the dot net platform. In the same way that ASP.NET MVC magically makes HTTP endpoints with minimal boilerplate, Actin tries to magically make complex (but well organized) systems with minimal boilerplate.

### Installing

* .Net Framework: Install-Package KC.Actin
* .Net Core: dotnet add package KC.Actin

## Why would I use Actin?

1. You need to repeatedly perform some task. (ie Check on some widget with some widget protocol, every 15 seconds)
2. You need to customize a series of tasks based on some sort of configuration data. (ie Check on every widget referenced in the database with its respective protocol, every 15 seconds)
3. You need these tasks to interact with each other in complex ways (ie Queue the widget data to be stored in the database, periodically check if the database is up, then batch write the data to the database).
4. You want to write as little code as possible.

Below is an asynchronous multi-threaded application that effectively coordinates all that in around 100 lines of code (minus talking to an actual database or actual widgets). Instantiation is automated. Dependencies are magically provided. Error logging is automated. Disposal is automated. All you need to do is write the app, and remember that each Actor is running independently so you need locks on shared resources (The MesssageQueue and Atom types are used for this below).

```C#
using KC.Actin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Example.Actin
{
    class Program
    {
        static void Main(string[] args)
        {
            new Director("log.txt").Run().Wait();
        }
    }

    [Singleton]
    class CacheTheWidgetConfigurations : Actor {
        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 5);

        Atom<List<WidgetConfig>> m_WidgetInfo = new Atom<List<WidgetConfig>>(new List<WidgetConfig>());
        public IEnumerable<WidgetConfig> WidgetInfo => m_WidgetInfo.Value;

        protected override async Task OnRun(ActorUtil util) {
            m_WidgetInfo.Value = new List<WidgetConfig> {
                new WidgetConfig { Id = 1, Name = "Widget One", Type = "TYPE1" }, 
                new WidgetConfig { Id = 2, Name = "Widget Two", Type = "TYPE2" }, 
            };
            await Task.FromResult(0);
        }
    }

    [Singleton]
    class ManageTheWidgetMonitors : Scene {
        [Singleton]
        CacheTheWidgetConfigurations widgetCache;

        protected override async Task<IEnumerable<Role>> CastActors(ActorUtil util, Dictionary<int, Actor> myActors) {
            await Task.FromResult(0);
            return widgetCache.WidgetInfo.Select(widgetInfo => new Role {
                Id = widgetInfo.Id,
                Type = getWidgetType(widgetInfo.Type)
            }).Where(x => x.Type != null);

            Type getWidgetType(string configType) {
                switch (configType) {
                    case "TYPE1":
                        return typeof(WidgetMonitor_Type1);
                    case "TYPE2":
                        return typeof(WidgetMonitor_Type2);
                    default:
                        util.Log.RealTime(null, this.ActorName, $"Unknown widget type: {configType}");
                        return null;
                }
            }
        }
    }

    public abstract class WidgetMonitor : Actor {

        protected abstract WidgetData CheckOnWidget(WidgetConfig info);

        [Singleton]
        CacheTheWidgetConfigurations widgetCache;
        [Singleton]
        PushWidgetDataToTheDatabase databasePusher;

        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 2);

        private Atom<string> name = new Atom<string>("Unknown");
        public override string ActorName => name.Value;

        protected override async Task OnRun(ActorUtil util) {
            var myInfo = widgetCache.WidgetInfo.First(x => x.Id == this.Id);

            name.Value = $"{myInfo.Name} :: {myInfo.Id}";

            var data = CheckOnWidget(myInfo);
            databasePusher.DataToPush.Enqueue(data);
            await Task.FromResult(0);
        }
    }

    [Instance]
    class WidgetMonitor_Type1 : WidgetMonitor {
        protected override WidgetData CheckOnWidget(WidgetConfig info) {
            return new WidgetData {
                Id = this.Id,
                IsAlive = new Random().Next(4) != 0,
            };
        }
    }

    [Instance]
    class WidgetMonitor_Type2 : WidgetMonitor {
        protected override WidgetData CheckOnWidget(WidgetConfig info) {
            return new WidgetData {
                Id = this.Id,
                IsAlive = new Random().Next(8) != 0,
            };
        }
    }

    [Singleton]
    class PushWidgetDataToTheDatabase : Actor {

        public MessageQueue<WidgetData> DataToPush = new MessageQueue<WidgetData>();

        protected override async Task OnRun(ActorUtil util) {
            if (DataToPush.TryDequeue(out var widgetData)) {
                Console.WriteLine($"SENT TO DATABASE: '{widgetData}'");
            }
            await Task.FromResult(0);
        }
    }

    public class WidgetData {
        public int Id { get; set; }
        public bool IsAlive { get; set; }

        public override string ToString() {
            return $"{Id} :: {(IsAlive ? "Alive" : "Dead")}";
        }
    }

    public class WidgetConfig {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }

}
```
