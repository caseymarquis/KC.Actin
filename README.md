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
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Example.Actin
{
    class Program
    {
        static void Main(string[] args)
        {
            new Director().Run(config => {
                config.Set_StandardLogOutputFolder("./log");
                //You can also use custom logging.
            }).Wait();
        }
    }

    /// <summary>
    /// This class pretends to grab some configuration data from a database and cache it.
    ///
    /// Because the class is marked with [Singleton], the director from above will automatically
    /// create an instance of it on start up. This instance can be used as a dependency in other
    /// Actors. (Though you can also use [Singleton/Instance/Etc] dependency injection attributes on non-actors.)
    /// </summary>
    [Singleton]
    class CacheTheWidgetConfigurations : Actor {
        /// <summary>
        /// You can optionally specify how often the Actor runs.
        /// The default is to run twice per second.
        /// </summary>
        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 5);

        /// <summary>
        /// An 'Atom' is just a simple wrapper around an object that ensures all access is locked (using a ReaderWriterLockSlim)
        /// Since WidgetInfo is public, and all Actors may be running in separate threads, this locking ensures
        /// we don't have any multithreading issues when other Actors access WidgetInfo.
        /// </summary>
        Atom<ImmutableList<WidgetConfig>> m_WidgetInfo = new Atom<ImmutableList<WidgetConfig>>(ImmutableList<WidgetConfig>.Empty);
        public ImmutableList<WidgetConfig> WidgetInfo => m_WidgetInfo.Value;

        protected override async Task OnRun(ActorUtil util) {
            //In reality, this would be updated from a database or config file:
            m_WidgetInfo.Value = new WidgetConfig[] {
                new WidgetConfig { Id = 1, Name = "Widget One", Type = "TYPE1" }, 
                new WidgetConfig { Id = 2, Name = "Widget Two", Type = "TYPE2" }, 
            }.ToImmutableList();
        }
    }

    /// <summary>
    /// Just like the first Actor, this 'Scene' will also be created on start up because
    /// it is marked with the [Singleton] attribute.
    /// 
    /// A scene is an Actor which dynamically instantiates other Actors at run time.
    /// </summary>
    [Singleton]
    class ManageTheWidgetMonitors : Scene {
        /// <summary>
        /// Before the 'Scene' is allowed to run, Actin will ensure that this
        /// field has been populated. Actin also supports more complex non-singleton
        /// dependencies with optional type checking on startup, and parent/child/sibling
        /// relationships.
        /// </summary>
        [Singleton] CacheTheWidgetConfigurations widgetCache;

        /// <summary>
        /// In order to dynamically start/stop Actors at run time,
        /// Actin needs to know which Actors should be running.
        /// 
        /// In the CastActors() function, a Scene returns this information,
        /// and then Actin takes care of dynamically starting/stopping the Scene's
        /// child actors as needed.
        /// </summary>
        protected override async Task<IEnumerable<Role>> CastActors(ActorUtil util, Dictionary<int, Actor> myActors) {
            return widgetCache.WidgetInfo.Select(widgetInfo => new Role {
                Id = widgetInfo.Id, //All Actors in a Scene must have a unique id. By default this is an int, but this can be customized with generic type arguments on the Scene class.
                Type = getWidgetType(widgetInfo.Type) //The type of the Actor is needed, unless the Scene only has a single type of child, and that type was given to Scene as a generic type argument.
            }).Where(x => x.Type != null);

            Type getWidgetType(string configType) {
                switch (configType) {
                    case "TYPE1":
                        return typeof(WidgetMonitor_Type1);
                    case "TYPE2":
                        return typeof(WidgetMonitor_Type2);
                    default:
                        //util.Log automatically includes the id and name of the Actor calling it.
                        util.Log.RealTime($"Unknown widget type: {configType}");
                        return null;
                }
            }
        }
    }

    /// <summary>
    /// Abstract classes may also have dependencies.
    /// </summary>
    public abstract class WidgetMonitor : Actor {
        [Singleton] CacheTheWidgetConfigurations widgetCache;
        [Singleton] PushWidgetDataToTheDatabase databasePusher;

        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 2);

        protected abstract WidgetData CheckOnWidget(WidgetConfig info);

        private Atom<string> name = new Atom<string>("Unknown");
        public override string ActorName => name.Value;

        protected override async Task OnRun(ActorUtil util) {
            var myInfo = widgetCache.WidgetInfo.First(x => x.Id == this.Id);

            name.Value = $"{myInfo.Name} :: {myInfo.Id}";

            //util also comes with several utility functions which
            //make profiling and detailed error logging easier.
            var data = util.Try("CheckWidget", () => CheckOnWidget(myInfo))
                .ErrorMessage("Widget check failed.")
                .SwallowExceptionWithoutCatch()
                .SkipProfilingIf(fasterThanXMilliseconds: 1000)
                .Execute();
            databasePusher.DataToPush.Enqueue(data);
        }
    }

    /// <summary>
    /// Notice that this class is marked with [Instance].
    /// Instance actors may be created and owned by other Actors.
    /// </summary>
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
        /// <summary>
        /// A message queue is a just simple wrapper around a list.
        /// It allows for concurrent operations, and provides some utility
        /// functions for dequeueing all messages.
        /// 
        /// Use of a MessageQueue ensures that Actors running in different threads
        /// are able to safely pass data to one another.
        /// </summary>
        public MessageQueue<WidgetData> DataToPush = new MessageQueue<WidgetData>();

        protected override async Task OnInit(ActorUtil util) {
            //This is run after dependencies are resolved,
            //but before the Actor is run.
            //Initialization is asynchronous, so there are no guarantees
            //that dependencies will have finished running their own OnInit functions.
            //
            //For this reason, MessageQueues should be used to pass data between Actors.
            //Dependencies can then handle those messages when they decide they are able to.
        }

        protected override async Task OnRun(ActorUtil util) {
            while (DataToPush.TryDequeue(out var widgetData)) {
                Console.WriteLine($"SENT TO DATABASE: '{widgetData}'");
            }
        }

        protected override async Task OnDispose(ActorUtil util) {
            //This is run before the actor is disposed of.
            //This will not normally run while OnRun() or OnInit() is running.
            //If OnRun or OnInit are locked up, OnDispose will eventually
            //be called without locking guarantees so that it at least has
            //a chance to dispose of a locked up Actor.
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
