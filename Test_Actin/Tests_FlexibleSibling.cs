using KC.Actin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin {
    public class FlexibleSiblingTests {
        public interface IHaveFamily {
            IHaveFamily Sibling { get; set; }
            IHaveFamily Parent { get; set; }
        }

        [Singleton]
        public class AllDependencies {
            public MessageQueue<IHaveFamily> Dependencies = new MessageQueue<IHaveFamily>();
        }

        public abstract class PocoWithParent : IOnInit, IHaveFamily {
            [FlexibleParent]
            public IHaveFamily Parent { get; set; }
            [FlexibleSibling]
            public IHaveFamily Sibling { get; set; }

            [Singleton]
            AllDependencies allDependencies;

            public async Task OnInit(ActorUtil util) {
                allDependencies.Dependencies.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        [Singleton]
        public class SingletonPocoRoot : PocoWithParent {
            [Instance]
            InstancePocoChild child;
            [Instance]
            InstanceActorChild actorChild;
        }

        [Instance]
        public class InstancePocoChild : PocoWithParent {
        }

        [Instance]
        public class InstanceActorChild : Actor, IHaveFamily {
            [FlexibleParent]
            public IHaveFamily Parent { get; set; }
            [FlexibleSibling]
            public IHaveFamily Sibling { get; set; }

            [Singleton]
            AllDependencies allDependencies;

            [Instance]
            InstancePocoChild child;

            protected override async Task OnRun(ActorUtil util) {
                await Task.FromResult(0);
            }

            protected override async Task OnInit(ActorUtil util) {
                allDependencies.Dependencies.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        [Singleton]
        public class SingletonActor : Actor, IHaveFamily {
            [FlexibleParent]
            public IHaveFamily Parent { get; set; }
            [FlexibleSibling]
            public IHaveFamily Sibling { get; set; }

            [Singleton]
            AllDependencies allDependencies;

            [Instance]
            InstanceActorChild actorChild;
            [Instance]
            InstancePocoChild child;

            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);
            protected async override Task OnRun(ActorUtil util) {
                await Task.FromResult(0);
            }

            protected override async Task OnInit(ActorUtil util) {
                allDependencies.Dependencies.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        [Singleton]
        public class SingletonScene : Scene<ActorInScene>, IHaveFamily {
            [FlexibleParent]
            public IHaveFamily Parent { get; set; }
            [FlexibleSibling]
            public IHaveFamily Sibling { get; set; }

            [Singleton]
            AllDependencies allDependencies;

            [Instance]
            InstanceActorChild actorChild;
            [Instance]
            InstancePocoChild child;

            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);
            protected override async Task<IEnumerable<Role>> CastActors(ActorUtil util, Dictionary<int, ActorInScene> myActors) {
                return await Task.FromResult(new Role[] {
                    new Role {
                        Id = 1,
                    },
                });
            }

            protected override async Task OnInit(ActorUtil util) {
                allDependencies.Dependencies.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        [Instance]
        public class ActorInScene : Actor, IHaveFamily {
            [FlexibleParent]
            public IHaveFamily Parent { get; set; }
            [FlexibleSibling]
            public IHaveFamily Sibling { get; set; }

            [FlexibleSibling]
            public InstanceActorChild actorSibling;
            [FlexibleSibling]
            public InstancePocoChild pocoSibling;

            [Singleton]
            AllDependencies allDependencies;

            [Instance]
            InstanceActorChild actorChild;
            [Instance]
            InstancePocoChild child;

            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);
            protected async override Task OnRun(ActorUtil util) {
                await Task.FromResult(0);
            }

            protected override async Task OnInit(ActorUtil util) {
                allDependencies.Dependencies.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        [Fact]
        public async Task TestFlexibleSiblings() {
            var director = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            director.Run(configure: async (util) => {
                var nestedTypes = typeof(FlexibleSiblingTests).GetNestedTypes();
                util.SetRootActorFilter(x => nestedTypes.Contains(x.Type));
                util.SetAssembliesToCheckForDependencies(Assembly.GetExecutingAssembly());
                await Task.FromResult(0);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await Task.Delay(500);
            director.Dispose();
            await Task.Delay(500);

            var allDependencies = director.GetSingleton<AllDependencies>().Dependencies.DequeueAll();

            //SingletonPocoRoot
            //  InstancePocoChild
            //  InstanceActorChild
            //      InstancePocoChild
            Assert.Single(allDependencies.Where(x => x is SingletonPocoRoot));
            Assert.Single(allDependencies.Where(x => x is InstancePocoChild && x.Parent is SingletonPocoRoot && x.Sibling is InstanceActorChild));
            Assert.Single(allDependencies.Where(x => x is InstanceActorChild && x.Parent is SingletonPocoRoot && x.Sibling is InstancePocoChild));
            Assert.Single(allDependencies.Where(x => x is InstancePocoChild && x.Parent is InstanceActorChild && x.Parent.Parent is SingletonPocoRoot && x.Sibling == null));

            //SingletonActor
            //  InstancePocoChild
            //  InstanceActorChild
            //      InstancePocoChild
            Assert.Single(allDependencies.Where(x => x is SingletonActor));
            Assert.Single(allDependencies.Where(x => x is InstancePocoChild && x.Parent is SingletonActor && x.Sibling is InstanceActorChild));
            Assert.Single(allDependencies.Where(x => x is InstanceActorChild && x.Parent is SingletonActor && x.Sibling is InstancePocoChild));
            Assert.Single(allDependencies.Where(x => x is InstancePocoChild && x.Parent is InstanceActorChild && x.Parent.Parent is SingletonActor && x.Sibling == null));

            //SingletonScene
            //  InstancePocoChild
            //  InstanceActorChild
            //      InstancePocoChild
            Assert.Single(allDependencies.Where(x => x is SingletonScene));
            Assert.Single(allDependencies.Where(x => x is InstancePocoChild && x.Parent is SingletonScene && x.Sibling is InstanceActorChild));
            Assert.Single(allDependencies.Where(x => x is InstanceActorChild && x.Parent is SingletonScene && x.Sibling is InstancePocoChild));
            Assert.Single(allDependencies.Where(x => x is InstancePocoChild && x.Parent is InstanceActorChild && x.Parent.Parent is SingletonScene && x.Sibling == null));
            //  ActorInScene
            //    InstancePocoChild
            //    InstanceActorChild
            //        InstancePocoChild
            Assert.Single(allDependencies.Where(x => x is InstancePocoChild && x.Parent is ActorInScene && x.Sibling is InstanceActorChild));
            Assert.Single(allDependencies.Where(x => x is InstanceActorChild && x.Parent is ActorInScene && x.Sibling is InstancePocoChild));
            Assert.Single(allDependencies.Where(x => x is InstancePocoChild && x.Parent is InstanceActorChild && x.Parent.Parent is ActorInScene && x.Sibling == null));

            Assert.Single(allDependencies.Where(x => x is ActorInScene && ((ActorInScene)x).pocoSibling != null && ((ActorInScene)x).actorSibling != null));
        }
    }
}
