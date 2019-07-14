﻿using KC.Actin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example.Actin
{
    class Program
    {
        static void Main(string[] args)
        {
            var director = new Director(new Logger());
            director.PrintGraphToDebug = true;
            //This will run until the user hits Q or Escape. (If Environment is interactive.)
            //Otherwise, it will run until director.Dispose() is called.
            //All Actors/Scenes marked with the Singleton attribute will be automatically created
            //and passed their dependencies. Other actors should be 
            director.Run(startUp_loopUntilSucceeds: true, startUp: async (util) => {
                //Run special startup code if needed, and manually add processes or dependencies.
                //This is useful if you need to do something like ensuring a database is migrated before
                //starting the application.
                Console.WriteLine("This runs before processes are automatically instantiated.");
                await Task.FromResult(0);
            }).Wait();

            //It's worth mentioning here that if you create an Actor to start/stop ASP.NET,
            //there is an extension method to add all Actin singletons as ASP.NET dependency injection services.
            //This allows you to natively access Actin dependencies in ASP.NET.
            //TODO: Write this extension and put a preview line of code here.
        }
    }

    /// <summary>
    /// This will generate a new number once per second.
    /// </summary>
    [Singleton]
    class GetWork : Actor {

        //Automatically created and passed arguments using reflection.
        [Singleton]
        private SaveWork saveWork;

        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 1);

        private Random r = new Random();
        protected async override Task OnRun(ActorUtil util) {
            var number = r.Next();
            Console.WriteLine(number);
            saveWork.WorkToSave.Enqueue(number);
            await Task.FromResult(0);
        }
    }

    /// <summary>
    /// This will write numbers to disk every 15 seconds.
    /// It will be automatically created using reflection,
    /// and passed to anything which needs a reference to it.
    /// </summary>
    [Singleton]
    class SaveWork : Actor {

        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 15);

        //This is just a thread safe queue with some utility functions on it.
        public MessageQueue<int> WorkToSave = new MessageQueue<int>();

        protected async override Task OnRun(ActorUtil util) {
            if (WorkToSave.TryDequeueAll(out var items)) {
                var sb = new StringBuilder();
                foreach (var item in items) {
                    sb.AppendLine(item.ToString());
                }
                await File.WriteAllTextAsync("./numbers.txt", sb.ToString());
            }
        }
    }

    [Singleton]
    class ASceneWithConcreteActors : Scene<PeerOne> {
        Random r = new Random();
        //This is run every 3 seconds by default, and is used to figure out which Actors need to be disposed or created:
        protected override async Task<IEnumerable<Role>> CastActors(ActorUtil util, Dictionary<int, PeerOne> myActors) {
            //Normally, you would check a database or some configuration data to figure out which Actors should be running.
            //We're just picking some random numbers instead. Ids can use types other than int. This is shown further down.
            //Note that the Scene also has an Id type, and could itself be managed by another scene, or have its own peers.

            //If the list of Ids differs from the list of running actors, the scene will resolve that difference.
            return await Task.FromResult(new Role[] {
                new Role { Id = r.Next(10) },
                new Role { Id = r.Next(10) },
                new Role { Id = r.Next(10) },
                new Role { Id = r.Next(10) },
                new Role { Id = r.Next(10) },
                new Role { Id = r.Next(10) },
                new Role { Id = r.Next(10) },
                new Role { Id = r.Next(10) },
                new Role { Id = r.Next(10) },
                new Role { Id = r.Next(10) },
            });
        }
    }

    //A non-singleton dependency must be marked with the Peer attribute so that we know to
    //cache its creation information on start up. While there are ways around this,
    //I think it's more consistent if this is explicit.
    [Peer]
    class PeerOne : Actor {
        //A dependency marked with Peer will be created once for each set of peers that it has.
        //When any peer is disposed, all related peers will automatically be disposed along with it.
        [Peer]
        public PeerTwo PeerTwo { get; private set; }
        [Peer]
        private PeerThree peerThree { get; set; }

        private int count = 0;
        protected override async Task OnRun(ActorUtil util) {
            count++;
            if (count % 2 == 0) {
                PeerTwo.Work.Enqueue(count);
            }
            if (count % 3 == 0) {
                peerThree.Work.Enqueue(count);
            }
            await Task.FromResult(0);
        }
    }

    [Peer]
    class PeerTwo : Actor {
        //Since PeerTwo is created as a dependency of PeerOne,
        //and PeerThree is a dependency of PeerOne,
        //the below PeerThree instance will be the same instance
        //as that on the above PeerOne class.
        [Peer]
        public PeerThree peerThree { get; private set; }

        public MessageQueue<int> Work = new MessageQueue<int>();

        protected override async Task OnRun(ActorUtil util) {
            while (Work.TryDequeue(out var number)) {
                Console.WriteLine($"{number} is divisible by 2.");
            }
            await Task.FromResult(0);
        }
    }

    [Peer]
    class PeerThree : Actor {
        public MessageQueue<int> Work = new MessageQueue<int>();

        protected override async Task OnRun(ActorUtil util) {
            while (Work.TryDequeue(out var number)) {
                Console.WriteLine($"{number} is divisible by 3.");
            }
            await Task.FromResult(0);
        }
    }

    [Singleton]
    class ASceneWithAbstractActors : Scene<TransformNumber> {
        protected override async Task<IEnumerable<Role>> CastActors(ActorUtil util, Dictionary<int, TransformNumber> myActors) {
            //The actor with id 2 will be repeatedly created, then disposed, then created.
            myActors.Values.Where(x => x.Id == 2).FirstOrDefault()?.Dispose();
            return await Task.FromResult(new Role[] {
                new Role {
                    Id = 1,
                    Type = typeof(PeerIncrement) //You would also check the database to see what this type should be.
                },
                new Role {
                    Id = 2,
                    Type = typeof(PeerDecrement)
                }
            });
        }
    }

    abstract class TransformNumber : Actor {
        protected abstract Task<int> Transform(ActorUtil util, int x);

        Random r = new Random();
        protected override async Task OnRun(ActorUtil util) {
            var x = r.Next(10);
            Console.WriteLine($"Number Transformed: {x} => {await Transform(util, x)}");
        }
    }

    [Peer]
    class PeerIncrement : TransformNumber {
        protected override async Task<int> Transform(ActorUtil util, int x) {
            return await Task.FromResult(++x);
        }
    }

    [Peer]
    class PeerDecrement : TransformNumber {
        protected override async Task<int> Transform(ActorUtil util, int x) {
            return await Task.FromResult(--x);
        }
    }

    [Singleton]
    class ASceneWithNoGenericConstraints : Scene {
        protected override async Task<IEnumerable<Role>> CastActors(ActorUtil util, Dictionary<int, Actor> myActors) {
            return await Task.FromResult(new Role[] {
                new Role {
                    Id = 1,
                    Type = typeof(PeerIncrement) //You would also check the database to see what this type should be.
                },
                new Role {
                    Id = 2,
                    Type = typeof(PeerDecrement)
                }
            });
        }
    }

    /// <summary>
    /// Under the hood, both Actor and Scene ids are generic.
    /// This means you're not stuck using ints.
    /// This is the most complex generic base class, which changes the type of both the Actor and Scene ids.
    /// All of a Scene's Actors must use the same type of id, but may otherwise may be different types
    /// (unless purposefully constrained by the generic arguments).
    /// </summary>
    [Singleton]
    class ASceneWithCustomizedTypes : Scene<Actor<Role<long>, long>, Role<long>, long, Role<string>, string> {
        public ASceneWithCustomizedTypes() {
        }

        protected override async Task<IEnumerable<Role<long>>> CastActors(ActorUtil util, Dictionary<long, Actor<Role<long>, long>> myActors) {
            return await Task.FromResult(new Role<long>[] {
                //These actors use a long for their id.
            }); 
        }
    }

    [Peer]
    class AnActorWithCustomizedTypes : Actor<Role<long>, long>
    {
        protected override Task OnRun(ActorUtil util)
        {
            throw new NotImplementedException();
        }
    }



    /// <summary>
    /// The simplest version of a logging class.
    /// </summary>
    class Logger : IActinLogger {
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