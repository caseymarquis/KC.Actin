using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace KC.Actin {
    /// <summary>
    /// An atom is a simple wrapper for atomically accessing an object.
    /// Internally, a ReaderWriterLockSlim is used.
    /// </summary>
    public class Atom<T> {
        /// <summary>
        /// Create a new Atom with a default T.
        /// </summary>
        public Atom() {
        }

        /// <summary>
        /// Create a new Atom with the specified object.
        /// </summary>
        public Atom(T value) {
            m_value = value;
        }

        private ReaderWriterLockSlim lockValue = new ReaderWriterLockSlim();
        private T m_value;
        /// <summary>
        /// The object or value stored by the Atom.
        /// </summary>
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

        /// <summary>
        /// Modify the internal value of the Atom with exclusive write access.
        /// </summary>
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
