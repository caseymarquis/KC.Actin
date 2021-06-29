using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace KC.Actin {
    public class Atom<T> {
        public Atom() {

        }

        public Atom(T value) {
            m_value = value;
        }

        private ReaderWriterLockSlim lockValue = new ReaderWriterLockSlim();
        private T m_value;
        public T Value {
            get {
                lockValue.EnterReadLock();
                try {
                    return m_value;
                }
                finally {
                    lockValue.ExitReadLock();
                }
            }
            set {
                lockValue.EnterWriteLock();
                try {
                    m_value = value;
                }
                finally {
                    lockValue.ExitWriteLock();
                }
            }
        }

        public T Modify(Func<T, T> transformValue) {
            lockValue.EnterWriteLock();
            try
            {
                m_value = transformValue(m_value);
                return m_value;
            }
            finally {
                lockValue.ExitWriteLock();
            }
        }
    }
}
