using KC.Actin;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin {
    public class Tests_Misc {
        [Fact]
        public async Task RequestAndAwaitRun() {
            var dir = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () => {
                await Task.Delay(4000);
                dir.Dispose();
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await dir.Run(config => {
                config.Set_AssembliesToCheckForDependencies(typeof(Caller).Assembly);
                var nestedTypes = typeof(Tests_Misc).GetNestedTypes();
                config.Set_RootActorFilter(inst => nestedTypes.Contains(inst.Type));
            });

            Assert.True(Caller.RunCount.Value >= 5);
            Assert.True(Callee.RunCount.Value >= Caller.RunCount.Value);
        }

        [Singleton]
        public class Caller : Actor {
            [Singleton] Callee callee;

            protected override TimeSpan RunInterval => new TimeSpan(0, 0, 0, 0, 50);
            public static Atom<int> RunCount = new Atom<int>();
            protected override async Task OnRun(ActorUtil util) {
                RunCount.Modify(x => x + 1);
                await callee.RequestAndAwaitRun();
            }
        }

        [Singleton]
        public class Callee : Actor {
            protected override TimeSpan RunInterval => new TimeSpan(1, 0, 0);

            public static Atom<int> RunCount = new Atom<int>();

            protected override async Task OnRun(ActorUtil util) {
                RunCount.Modify(x => x + 1);
                await Task.FromResult(0);
            }
        }
    }
}
