using KC.Actin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin {
    public class SiblingTests {
        public interface IHasSibling {
            IHasSibling Sibling { get; }
        }

        [Singleton]
        public class AllInjectedDependencies {
            public MessageQueue<IHasSibling> InjectedDependencies = new MessageQueue<IHasSibling>();
        }

        public abstract class InjectedPoco : IOnInit, IHasSibling {
            [Singleton]
            AllInjectedDependencies allDependencies;
            public abstract IHasSibling Sibling { get; }

            public async Task OnInit(ActorUtil util) {
                allDependencies.InjectedDependencies.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        public abstract class ActorChild : Actor, IHasSibling {
            [Singleton]
            AllInjectedDependencies allDependencies;

            public abstract IHasSibling Sibling { get; }

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

            public override IHasSibling Sibling => null;
        }

        [Instance]
        public class SingletonPocoPocoChild : InjectedPoco {
            [Sibling]
            private SingletonPocoActorChild sibling; 
            public override IHasSibling Sibling => sibling;
        }

        [Instance]
        public class SingletonPocoActorChild : ActorChild {
            [Sibling]
            private SingletonPocoPocoChild sibling; 
            public override IHasSibling Sibling => sibling;
        }

        [Singleton]
        public class SingletonActor : Actor, IHasSibling {
            public IHasSibling Sibling => null;

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
            [Sibling]
            private SingletonActorActorChild sibling;
            public override IHasSibling Sibling => sibling;
        }

        [Instance]
        public class SingletonActorActorChild : ActorChild {
            [Sibling]
            private SingletonActorPocoChild sibling;
            public override IHasSibling Sibling => sibling;
        }

        [Fact]
        public async Task TestSiblings() {
            var director = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            director.Run(startUp_loopUntilSucceeds: false, configure: async (util) => {
                var nestedTypes = typeof(SiblingTests).GetNestedTypes();
                util.SetRootActorFilter(x => nestedTypes.Contains(x.Type));
                await Task.FromResult(0);
            }, assembliesToCheckForDI: Assembly.GetExecutingAssembly());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await Task.Delay(500);
            director.Dispose();
            await Task.Delay(500);

            var allDependencies = director.GetSingleton<AllInjectedDependencies>().InjectedDependencies.DequeueAll();

            Assert.Single(allDependencies.Where(x => x is SingletonPoco));
            Assert.Single(allDependencies.Where(x => x is SingletonPocoPocoChild && x.Sibling is SingletonPocoActorChild));
            Assert.Single(allDependencies.Where(x => x is SingletonPocoActorChild && x.Sibling is SingletonPocoPocoChild));

            Assert.Single(allDependencies.Where(x => x is SingletonActor));
            Assert.Single(allDependencies.Where(x => x is SingletonActorPocoChild && x.Sibling is SingletonActorActorChild));
            Assert.Single(allDependencies.Where(x => x is SingletonActorActorChild && x.Sibling is SingletonActorPocoChild));
        }
    }
}
