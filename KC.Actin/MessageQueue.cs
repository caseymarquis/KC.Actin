using System;
using System.Collections.Generic;
using System.Linq;
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

        public bool Any(Func<T, bool> predicate) {
            lock (lockList) {
                return list.Any(predicate);
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

        /// <summary>
        /// Place this message at the front of the queue.
        /// </summary>
        public void Enqueue_InFront(T message) {
            lock (lockList) {
                while (list.Count > m_MaxMessages) {
                    list.RemoveAt(0);
                }
                list.Insert(0, message);
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

        /// <summary>
        /// Place these messages at the front of the queue.
        /// </summary>
        public void EnqueueRange_InFront(IEnumerable<T> messages) {
            lock (lockList) {
                while (list.Count > m_MaxMessages) {
                    list.RemoveAt(0);
                }
                list.InsertRange(0, messages);
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

        public IEnumerable<T> PeekAll() {
            lock (lockList) {
                return list.ToArray();
            }
        }

        public bool TryPeek(out T result) {
            lock (lockList) {
                if (list.Any()) {
                    result = list.First();
                    return true;
                }
            }
            result = default;
            return false;
        }
    }
}
