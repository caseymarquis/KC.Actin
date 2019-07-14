using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin
{
    public class ActorDisposeHandle
    {
        Func<ActorUtil, Task> actuallyDisposeProcess;
        private Actor_SansType process; //This is really just here for debugging. It's not used for anything.

        public ActorDisposeHandle(Func<ActorUtil, Task> _actuallyDisposeProcess, Actor_SansType _process) {
            this.actuallyDisposeProcess = _actuallyDisposeProcess;
            this.process = _process;
        }

        public async Task DisposeProcess(ActorUtil util) {
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

        public string ProcessName => process?.ActorName ?? "null";
    }
}
