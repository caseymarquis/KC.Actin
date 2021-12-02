using KC.Actin;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin
{
    public class ClockTests
    {
        [Singleton]
        public class ManualStepTester : Actor {
            protected override TimeSpan RunDelay => new TimeSpan(5, 0, 0);

            public Atom<int> TimesRunAtom = new Atom<int>();

            protected async override Task OnRun(ActorUtil util) {
                TimesRunAtom.Value += 1;
                await Task.FromResult(0);
                return;
            }
        }

        [Singleton]
        public class SpeedUpTester : Actor {
            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 200);

            public Atom<int> TimesRunAtom = new Atom<int>();

            protected async override Task OnRun(ActorUtil util) {
                TimesRunAtom.Value += 1;
                await Task.FromResult(0);
                return;
            }
        }

        [Singleton]
        public class DoIgnoreSimulatedTime : Actor {
            public override bool IgnoreSimulatedTime => true;

            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 200);

            public Atom<int> TimesRunAtom = new Atom<int>();

            protected async override Task OnRun(ActorUtil util) {
                TimesRunAtom.Value += 1;
                await Task.FromResult(0);
                return;
            }
        }

        [Singleton]
        public class DoNotIgnoreSimulatedTime : Actor {
            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 200);

            public Atom<int> TimesRunAtom = new Atom<int>();

            protected async override Task OnRun(ActorUtil util) {
                TimesRunAtom.Value += 1;
                await Task.FromResult(0);
                return;
            }
        }

        [Fact]
        public async Task ManuallyStepThroughTime()
        {
            var director = new Director();
            var startTime = new DateTimeOffset(new DateTime(1970, 1, 1), new TimeSpan());
            var clock = director.Clock;
            clock.Simulate(startTime, 0);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            director.Run(configure: async (util) => {
                var nestedTypes = typeof(ClockTests).GetNestedTypes();
                util.Set_RootActorFilter(x => nestedTypes.Contains(x.Type));
                util.Set_AssembliesToCheckForDependencies(Assembly.GetExecutingAssembly());
                await Task.FromResult(0);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await Task.Delay(700);
            var timeTester = director.GetSingleton<ManualStepTester>();
            Assert.Equal(1, timeTester.TimesRunAtom.Value);
            clock.Simulate(clock.Now.Add(new TimeSpan(5, 1, 0)), 0);
            await Task.Delay(100);
            Assert.Equal(2, timeTester.TimesRunAtom.Value);
            clock.Simulate(clock.Now.Add(new TimeSpan(5, 1, 0)), 0);
            await Task.Delay(100);
            Assert.Equal(3, timeTester.TimesRunAtom.Value);
            clock.Simulate(clock.Now.Add(new TimeSpan(5, 1, 0)), 0);
            await Task.Delay(100);
            Assert.Equal(4, timeTester.TimesRunAtom.Value);
        }

        [Fact]
        public async Task SpeedUpTime() {
            var normalTimeDirector = new Director();
            var spedUpDirector = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            normalTimeDirector.Run(configure: async (util) => {
                var nestedTypes = typeof(ClockTests).GetNestedTypes();
                util.Set_RootActorFilter(x => nestedTypes.Contains(x.Type));
                util.Set_AssembliesToCheckForDependencies(Assembly.GetExecutingAssembly());
                await Task.FromResult(0);
            });
            spedUpDirector.Run(configure: async (util) => {
                var nestedTypes = typeof(ClockTests).GetNestedTypes();
                util.Set_RootActorFilter(x => nestedTypes.Contains(x.Type));
                util.Set_AssembliesToCheckForDependencies(Assembly.GetExecutingAssembly());
                await Task.FromResult(0);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await Task.Delay(100);
            var normal = normalTimeDirector.GetSingleton<SpeedUpTester>();
            var spedUp = spedUpDirector.GetSingleton<SpeedUpTester>();

            //Reset to a baseline:
            normal.TimesRunAtom.Value = 0;
            spedUp.TimesRunAtom.Value = 0;

            var clock = spedUpDirector.Clock;
            clock.Simulate(null, 10);

            await Task.Delay(3000);
            var normalTimes = normal.TimesRunAtom.Value;
            var spedUpTimes = spedUp.TimesRunAtom.Value;

            //We don't expect it to be exact, as the Director has a finite resolution, but it should be in the ballpark:
            Assert.True(5*normalTimes < spedUpTimes);
            Assert.True(15*normalTimes > spedUpTimes);
        }

        [Fact]
        public async Task IgnoreSimulatedTime() {
            var director = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            director.Run(configure: async (util) => {
                var nestedTypes = typeof(ClockTests).GetNestedTypes();
                util.Set_RootActorFilter(x => nestedTypes.Contains(x.Type));
                util.Set_AssembliesToCheckForDependencies(Assembly.GetExecutingAssembly());
                await Task.FromResult(0);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await Task.Delay(100);
            var normal = director.GetSingleton<DoIgnoreSimulatedTime>();
            var spedUp = director.GetSingleton<DoNotIgnoreSimulatedTime>();

            //Reset to a baseline:
            normal.TimesRunAtom.Value = 0;
            spedUp.TimesRunAtom.Value = 0;

            var clock = director.Clock;
            clock.Simulate(null, 10);

            await Task.Delay(3000);
            var normalTimes = normal.TimesRunAtom.Value;
            var spedUpTimes = spedUp.TimesRunAtom.Value;

            //We don't expect it to be exact, as the Director has a finite resolution, but it should be in the ballpark:
            Assert.True(5 * normalTimes < spedUpTimes);
            Assert.True(15 * normalTimes > spedUpTimes);
        }

    }
}
