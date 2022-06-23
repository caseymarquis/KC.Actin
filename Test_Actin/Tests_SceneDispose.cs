using KC.Actin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin
{
    public class SceneDisposeTests
    {
        [Singleton]
        public class TheScene : Scene<SomeInstanceType> {
            protected override TimeSpan RunInterval => new TimeSpan(0, 0, 0, 0, 10);

            public MessageQueue<int> Disposed = new MessageQueue<int>();
            public MessageQueue<int> Initialized = new MessageQueue<int>();

            protected override async Task<IEnumerable<Role>> CastActors(ActorUtil util, Dictionary<int, SomeInstanceType> myActors) {
                return await Task.FromResult(new Role[] {
                new Role {
                    Id = 1,
                },
                new Role {
                    Id = 2,
                },
            });
            }
        }

        [Instance]
        public class SomeInstanceType : Actor {
            protected override TimeSpan RunInterval => new TimeSpan(0, 0, 0, 0, 10);

            [FlexibleParent]
            private TheScene theScene;

            protected override async Task OnInit(ActorUtil util) {
                theScene.Initialized.Enqueue(this.Id); 
                await Task.FromResult(0);
            }

            protected async override Task OnRun(ActorUtil util) {
                this.Dispose();
                await Task.FromResult(0);
                return;
            }

            protected override async Task OnDispose(ActorUtil util) {
                theScene.Disposed.Enqueue(this.Id);
                await Task.FromResult(0);
            }

        }

        [Fact]
        public async Task BuildAndRunAScene()
        {
            var director = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            director.Run(configure: async (util) => {
                var nestedTypes = typeof(SceneDisposeTests).GetNestedTypes();
                util.Set_RootActorFilter(x => nestedTypes.Contains(x.Type));
                util.Set_AssembliesToCheckForDependencies(Assembly.GetExecutingAssembly());
                await Task.FromResult(0);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            await Task.Delay(2500);
            var sceneData = director.GetSingleton<TheScene>();
            if (!sceneData.Initialized.TryDequeueAll(out var initialized)) {
                throw new Exception("No actors were run in the scene.");
            }
            Assert.Contains(1, initialized);
            Assert.Contains(2, initialized);
            Assert.True(initialized.Length > 10);

            if (!sceneData.Disposed.TryDequeueAll(out var disposed)) {
                throw new Exception("No actors were run in the scene.");
            }
            Assert.Contains(1, disposed);
            Assert.Contains(2, disposed);
            Assert.True(disposed.Length > 10);
        }
    }
}
