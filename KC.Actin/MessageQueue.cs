using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin {
    public class MessageQueue<T> {
        private object lockList = new object();
        private List<T> list = new List<T>();

        private object lockMaxMessages = new object();
        private int m_MaxMessages = int.MaxValue;
        public int MaxItems {
            get {
                lock (lockMaxMessages) {
                    return m_MaxMessages;
                }
            }
            set {
                m_MaxMessages = value;
            }
        }

        public bool Any() {
            lock (lockList) {
                return list.Count > 0;
            }
        }

        public void Enqueue(T message) {
            lock (lockList) {
                list.Add(message);
                while (list.Count > m_MaxMessages) {
                    list.RemoveAt(0);
                }
            }
        }

        public void EnqueueRange(IEnumerable<T> messages) {
            lock (lockList) {
                list.AddRange(messages);
                while (list.Count > m_MaxMessages) {
                    list.RemoveAt(0);
                }
            }
        }

        public int Count {
            get {
                lock (lockList) {
                    return list.Count;
                }
            }
        }

        public T[] DequeueAll() {
            lock (lockList) {
                if (list.Count == 0) {
                    return Array.Empty<T>();
                }
                var messages = list.ToArray();
                list.Clear();
                return messages;
            }
        }

        /// <summary>
        /// Returns true if there are any messages to process.
        /// </summary>
        public bool TryDequeueAll(out T[] messages) {
            messages = DequeueAll();
            return messages.Length > 0;
        }

        /// <summary>
        /// Returns true if a message was available.
        /// </summary>
        public bool TryDequeue(out T message) {
            lock (lockList) {
                if (list.Count == 0) {
                    message = default(T);
                    return false;
                }
                message = list[0];
                list.RemoveAt(0);
                return true;
            }
        }
    }
}
