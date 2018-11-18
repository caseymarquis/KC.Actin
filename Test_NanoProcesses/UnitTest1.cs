using KC.NanoProcesses;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test_NanoProcesses
{

    public class ProcManual : NanoProcess {
        public override string ProcessName => nameof(ProcManual);
        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);
        protected async override Task OnDispose(NpUtil util) {
            await Task.FromResult(0);
            return;
        }
        protected async override Task OnInit(NpUtil util) {
            await Task.FromResult(0);
            return;
        }
        protected async override Task OnRun(NpUtil util) {
            this.ManualRan = true;
            await Task.FromResult(0);
            return;
        }

        private object lockEverything = new object();
        private bool m_ManualRan;
        public bool ManualRan {
            get {
                lock (lockEverything) {
                    return m_ManualRan;
                }
            }
            set {
                lock (lockEverything) {
                    m_ManualRan = value;
                }
            }
        }

        private bool m_DIRan;
        public bool DIRan {
            get {
                lock (lockEverything) {
                    return m_DIRan;
                }
            }
            set {
                lock (lockEverything) {
                    m_DIRan = value;
                }
            }
        }
    }

    [NanoDI]
    public class ProcDI : NanoProcess{
        ProcManual procManual;
        public ProcDI(ProcManual _procManual) {
            this.procManual = _procManual;
        }

        public override string ProcessName => nameof(ProcDI);
        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 10);
        protected async override Task OnDispose(NpUtil util) {
            await Task.FromResult(0);
            return;
        }
        protected async override Task OnInit(NpUtil util) {
            await Task.FromResult(0);
            return;
        }
        protected async override Task OnRun(NpUtil util) {
            procManual.DIRan = true;
            await Task.FromResult(0);
            return;
        }
    }

    public class UnitTest1
    {
        [Fact]
        public async Task RunManualAndDIProcs()
        {
            var manager = new NanoProcessManager(null);
            var procManual = new ProcManual();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            manager.Run(startUp: async (util) => {
                manager.AddProcessAndDependency(procManual);
                await Task.FromResult(0);
            }, assembliesToCheckForDI: Assembly.GetExecutingAssembly());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            await Task.Delay(250);
            Assert.True(procManual.ManualRan, "Manually added process did not run within 250ms.");
            Assert.True(procManual.DIRan, "DI added process did not run within 250ms.");
        }

    }
}
