using KC.Actin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin
{
    public class SceneTests
    {
        [Singleton]
        public class TheScene : Scene<SomeInstanceType> {
            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);

            public MessageQueue<int> RunningIds = new MessageQueue<int>();

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
            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);

            [FlexibleParent]
            private TheScene theScene;

            protected async override Task OnRun(ActorUtil util) {
                theScene.RunningIds.Enqueue(this.Id);
                await Task.FromResult(0);
                return;
            }
        }

        [Fact]
        public async Task BuildAndRunAScene()
        {
            var director = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            director.Run(startUp_loopUntilSucceeds: false, startUp: async (util) => {
                var nestedTypes = typeof(SceneTests).GetNestedTypes();
                util.FilterRootActors(x => nestedTypes.Contains(x.Type));
                await Task.FromResult(0);
            }, assembliesToCheckForDI: Assembly.GetExecutingAssembly());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            await Task.Delay(500);
            var sceneData = director.GetSingleton<TheScene>();
            if (!sceneData.RunningIds.TryDequeueAll(out var ids)) {
                throw new Exception("No actors were run in the scene.");
            }
            Assert.Contains(1, ids);
            Assert.Contains(2, ids);
        }
    }
}
