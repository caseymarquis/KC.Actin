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
            new Director("./log").Run().Wait();
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
