using KC.Actin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin {
    public class ParentTests {
        public interface IHasParent {
            IHasParent Parent { get; }
        }

        [Singleton]
        public class AllInjectedDependencies {
            public MessageQueue<IHasParent> InjectedDependencies = new MessageQueue<IHasParent>();
        }

        public abstract class InjectedPoco : IOnInit, IHasParent {
            [Singleton]
            AllInjectedDependencies allDependencies;
            public abstract IHasParent Parent { get; }

            public async Task OnInit(ActorUtil util) {
                allDependencies.InjectedDependencies.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        public abstract class ActorChild : Actor, IHasParent {
            [Singleton]
            AllInjectedDependencies allDependencies;

            public abstract IHasParent Parent { get; }

            protected override async Task OnInit(ActorUtil util) {
                allDependencies.InjectedDependencies.Enqueue(this);
                await Task.FromResult(0);
            }

            protected override async Task OnRun(ActorUtil util) {
                await Task.FromResult(0);
            }
        }

        [Singleton]
        public class SingletonPoco : InjectedPoco {
            [Instance]
            SingletonPocoPocoChild child;
            [Instance]
            SingletonPocoActorChild actorChild;

            public override IHasParent Parent => null;
        }

        [Instance]
        public class SingletonPocoPocoChild : InjectedPoco {
            [Parent]
            private SingletonPoco parent; 
            public override IHasParent Parent => parent;
        }

        [Instance]
        public class SingletonPocoActorChild : ActorChild {
            [Parent]
            private SingletonPoco parent; 
            public override IHasParent Parent => parent;
        }

        [Singleton]
        public class SingletonActor : Actor, IHasParent {
            public IHasParent Parent => null;

            [Singleton]
            AllInjectedDependencies allDependencies;

            [Instance]
            SingletonActorPocoChild child;
            [Instance]
            SingletonActorActorChild actorChild;

            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);

            protected override async Task OnInit(ActorUtil util) {
                allDependencies.InjectedDependencies.Enqueue(this);
                await Task.FromResult(0);
            }

            protected async override Task OnRun(ActorUtil util) {
                await Task.FromResult(0);
            }

        }

        [Instance]
        public class SingletonActorPocoChild : InjectedPoco {
            [Parent]
            private SingletonActor parent;
            public override IHasParent Parent => parent;
        }

        [Instance]
        public class SingletonActorActorChild : ActorChild {
            [Parent]
            private SingletonActor parent;
            public override IHasParent Parent => parent;
        }

        [Fact]
        public async Task TestParents() {
            var director = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            director.Run(startUp_loopUntilSucceeds: false, startUp: async (util) => {
                var nestedTypes = typeof(ParentTests).GetNestedTypes();
                util.FilterRootActors(x => nestedTypes.Contains(x.Type));
                await Task.FromResult(0);
            }, assembliesToCheckForDI: Assembly.GetExecutingAssembly());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await Task.Delay(250);
            director.Dispose();
            await Task.Delay(250);

            var allDependencies = director.GetSingleton<AllInjectedDependencies>().InjectedDependencies.DequeueAll();

            Assert.Single(allDependencies.Where(x => x is SingletonPoco));
            Assert.Single(allDependencies.Where(x => x is SingletonPocoPocoChild && x.Parent is SingletonPoco));
            Assert.Single(allDependencies.Where(x => x is SingletonPocoActorChild && x.Parent is SingletonPoco));

            Assert.Single(allDependencies.Where(x => x is SingletonActor));
            Assert.Single(allDependencies.Where(x => x is SingletonActorPocoChild && x.Parent is SingletonActor));
            Assert.Single(allDependencies.Where(x => x is SingletonActorActorChild && x.Parent is SingletonActor));
        }
    }
}
