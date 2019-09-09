using KC.Actin;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin
{
    public class SingletonTests
    {
        [Instance]
        public class Poco {
        }

        public class ProcManual : Actor {
            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);

            public MessageQueue<int> SelfRan = new MessageQueue<int>();
            public MessageQueue<int> DiRan = new MessageQueue<int>();

            [Instance]
            public Poco AutoPoco;
            public Poco ManualPocoLeaveNull;

            protected async override Task OnRun(ActorUtil util) {
                SelfRan.Enqueue(0);
                await Task.FromResult(0);
            }
        }

        [Singleton]
        public class ProcDI : Actor {
            [Singleton]
            ProcManual procManual;

            protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);
            protected async override Task OnRun(ActorUtil util) {
                procManual.DiRan.Enqueue(1);
                await Task.FromResult(0);
                return;
            }
        }

        [Fact]
        public async Task BuildAndRunASingleton()
        {
            var director = new Director();
            var procManual = new ProcManual();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            director.Run(configure: async (util) => {
                var nestedTypes = typeof(SingletonTests).GetNestedTypes();
                util.SetRootActorFilter(x => nestedTypes.Contains(x.Type));
                util.SetAssembliesToCheckForDependencies(Assembly.GetExecutingAssembly());
                director.AddSingletonDependency(procManual);
                await Task.FromResult(0);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            await Task.Delay(500);
            Assert.True(procManual.SelfRan.Any(), "Manually added process did not run within 250ms.");
            Assert.True(procManual.DiRan.Any(), "DI added process did not run within 250ms.");
            Assert.True(procManual.AutoPoco != null, "Manually added process did not have dependency Poco instantiated.");
            Assert.Null(procManual.ManualPocoLeaveNull);
        }
    }
}
