using KC.Actin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin {
    public class DisposeTests {
        public interface IWasDisposed {
            IWasDisposed Parent { get; set; }
        }

        [Singleton]
        public class DisposedList {
            public MessageQueue<IWasDisposed> ShouldHaveBeenDisposed = new MessageQueue<IWasDisposed>();
        }

        public abstract class DisposablePoco : IDisposable, IWasDisposed {
            [FlexibleParent]
            public IWasDisposed Parent { get; set; }

            [Singleton]
            DisposedList disposedList;

            public void Dispose() {
                disposedList.ShouldHaveBeenDisposed.Enqueue(this);
            }
        }

        [Singleton]
        public class SingletonPocoRoot : DisposablePoco {
            [Instance]
            InstancePocoChild child;
            [Instance]
            InstanceActorChild actorChild;
        }

        [Instance]
        public class InstancePocoChild : DisposablePoco {
        }

        [Instance]
        public class InstanceActorChild : Actor, IWasDisposed {
            [FlexibleParent]
            public IWasDisposed Parent { get; set; }

            [Singleton]
            DisposedList disposedList;

            [Instance]
            InstancePocoChild child;

            protected override async Task OnRun(ActorUtil util) {
                await Task.FromResult(0);
            }

            protected override async Task OnDispose(ActorUtil util) {
                disposedList.ShouldHaveBeenDisposed.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        [Singleton]
        public class SingletonActor : Actor, IWasDisposed {
            [FlexibleParent]
            public IWasDisposed Parent { get; set; }

            [Singleton]
            DisposedList disposedList;

            [Instance]
            InstanceActorChild actorChild;
            [Instance]
            InstancePocoChild child;

            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);
            protected async override Task OnRun(ActorUtil util) {
                await Task.FromResult(0);
            }

            protected override async Task OnDispose(ActorUtil util) {
                disposedList.ShouldHaveBeenDisposed.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        [Singleton]
        public class SingletonScene : Scene<ActorInScene>, IWasDisposed {
            [FlexibleParent]
            public IWasDisposed Parent { get; set; }

            [Singleton]
            DisposedList disposedList;

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

            protected override async Task OnDispose(ActorUtil util) {
                disposedList.ShouldHaveBeenDisposed.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        [Instance]
        public class ActorInScene : Actor, IWasDisposed {
            [FlexibleParent]
            public IWasDisposed Parent { get; set; }

            [Singleton]
            DisposedList disposedList;

            [Instance]
            InstanceActorChild actorChild;
            [Instance]
            InstancePocoChild child;

            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);
            protected async override Task OnRun(ActorUtil util) {
                await Task.FromResult(0);
            }

            protected override async Task OnDispose(ActorUtil util) {
                disposedList.ShouldHaveBeenDisposed.Enqueue(this);
                await Task.FromResult(0);
            }
        }

        [Fact]
        public async Task DisposeThings() {
            var director = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            director.Run(configure: async (util) => {
                var nestedTypes = typeof(DisposeTests).GetNestedTypes();
                util.Set_RootActorFilter(x => nestedTypes.Contains(x.Type));
                util.Set_AssembliesToCheckForDependencies(Assembly.GetExecutingAssembly());
                await Task.FromResult(0);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await Task.Delay(500);
            director.Dispose();
            await Task.Delay(500);

            var shouldHaveDisposeds = director.GetSingleton<DisposedList>().ShouldHaveBeenDisposed.DequeueAll();

            //SingletonPocoRoot
            //  InstancePocoChild
            //  InstanceActorChild
            //      InstancePocoChild
            Assert.Single(shouldHaveDisposeds.Where(x => x is SingletonPocoRoot));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstancePocoChild && x.Parent is SingletonPocoRoot));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstanceActorChild && x.Parent is SingletonPocoRoot)); Assert.Single(shouldHaveDisposeds.Where(x => x is InstancePocoChild && x.Parent is InstanceActorChild && x.Parent.Parent is SingletonPocoRoot));

            //SingletonActor
            //  InstancePocoChild
            //  InstanceActorChild
            //      InstancePocoChild
            Assert.Single(shouldHaveDisposeds.Where(x => x is SingletonActor));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstancePocoChild && x.Parent is SingletonActor));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstanceActorChild && x.Parent is SingletonActor));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstancePocoChild && x.Parent is InstanceActorChild && x.Parent.Parent is SingletonActor));

            //SingletonScene
            //  InstancePocoChild
            //  InstanceActorChild
            //      InstancePocoChild
            Assert.Single(shouldHaveDisposeds.Where(x => x is SingletonScene));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstancePocoChild && x.Parent is SingletonScene));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstanceActorChild && x.Parent is SingletonScene));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstancePocoChild && x.Parent is InstanceActorChild && x.Parent.Parent is SingletonScene));
            //  ActorInScene
            //    InstancePocoChild
            //    InstanceActorChild
            //        InstancePocoChild
            Assert.Single(shouldHaveDisposeds.Where(x => x is ActorInScene));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstancePocoChild && x.Parent is ActorInScene));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstanceActorChild && x.Parent is ActorInScene));
            Assert.Single(shouldHaveDisposeds.Where(x => x is InstancePocoChild && x.Parent is InstanceActorChild && x.Parent.Parent is ActorInScene));
        }
    }
}
