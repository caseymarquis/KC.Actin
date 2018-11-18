using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KC.NanoProcesses
{
    public class NanoProcessDisposeHandle
    {
        Func<NpUtil, Task> actuallyDisposeProcess;
        private NanoProcess process; //This is really just here for debugging. It's not used for anything.

        public NanoProcessDisposeHandle(Func<NpUtil, Task> _actuallyDisposeProcess, NanoProcess _process) {
            this.actuallyDisposeProcess = _actuallyDisposeProcess;
            this.process = _process;
        }

        public async Task DisposeProcess(NpUtil util) {
            await actuallyDisposeProcess(util);
        }

        private object lockEverything = new object();
        private bool m_MustDispose;
        public bool MustDispose {
            get {
                lock (lockEverything) {
                    return m_MustDispose;
                }
            }
            set {
                lock (lockEverything) {
                    m_MustDispose = value;
                }
            }
        }

        public string ProcessName => process?.ProcessName ?? "null";
    }
}
