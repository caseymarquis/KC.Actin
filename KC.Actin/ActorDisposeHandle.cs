using KC.Actin.ActorUtilNS;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin
{
    public class ActorDisposeHandle
    {
        Func<Func<DispatchData>, Task> actuallyDisposeProcess;
        private Actor_SansType process; //This is really just here for debugging. It's not used for anything.

        public ActorDisposeHandle(Func<Func<DispatchData>, Task> _actuallyDisposeProcess, Actor_SansType _process) {
            this.actuallyDisposeProcess = _actuallyDisposeProcess;
            this.process = _process;
        }

        public async Task DisposeProcess(Func<DispatchData> getDispatchData) {
            await actuallyDisposeProcess(getDispatchData);
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
