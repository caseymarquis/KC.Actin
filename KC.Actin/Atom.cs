using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin {
    public class Atom<T> {
        public Atom() {

        }

        public Atom(T value) {
            m_value = value;
        }

        private object lockValue = new object();
        private T m_value;
        public T Value {
            get {
                lock (lockValue) {
                    return m_value;
                }
            }
            set {
                lock (lockValue) {
                    m_value = value;
                }
            }
        }
    }
}
