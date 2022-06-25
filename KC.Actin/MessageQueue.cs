using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KC.Actin {
    /// <summary>
    /// A threadsafe queue for passing messages between actors.
    /// This class contains some utilities for convenience.
    /// In the future, if Actin implements IPC, then this class may
    /// be used to facilitate message passing across processes.
    /// </summary>
    public class MessageQueue<T> {
        private object lockList = new object();
        private List<T> list = new List<T>();

        private object lockMaxMessages = new object();
        private int m_MaxMessages = int.MaxValue;
        /// <summary>
        /// The maximum number of messages which the queue can hold. If more messages are received,
        /// then the oldest messages above this number will be dropped.
        /// </summary>
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

        /// <summary>
        /// Return true if the queue contains any messages.
        /// </summary>
        public bool Any() {
            lock (lockList) {
                return list.Count > 0;
            }
        }

        /// <summary>
        /// Return true if the queue contains any messages matching the predicate.
        /// </summary>
        public bool Any(Func<T, bool> predicate) {
            lock (lockList) {
                return list.Any(predicate);
            }
        }

        /// <summary>
        /// Add a message to the end of the queue.
        /// </summary>
        public void Enqueue(T message) {
            lock (lockList) {
                list.Add(message);
                while (list.Count > m_MaxMessages) {
                    list.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Place a message at the front of the queue.
        /// </summary>
        public void Enqueue_InFront(T message) {
            lock (lockList) {
                while (list.Count > m_MaxMessages) {
                    list.RemoveAt(0);
                }
                list.Insert(0, message);
            }
        }

        /// <summary>
        /// Add multiple messages to the end of the queue.
        /// </summary>
        public void EnqueueRange(IEnumerable<T> messages) {
            lock (lockList) {
                list.AddRange(messages);
                while (list.Count > m_MaxMessages) {
                    list.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Add multiple messages to the front of the queue.
        /// </summary>
        public void EnqueueRange_InFront(IEnumerable<T> messages) {
            lock (lockList) {
                while (list.Count > m_MaxMessages) {
                    list.RemoveAt(0);
                }
                list.InsertRange(0, messages);
            }
        }

        /// <summary>
        /// The number of unprocessed messages in the queue.
        /// </summary>
        public int Count {
            get {
                lock (lockList) {
                    return list.Count;
                }
            }
        }

        /// <summary>
        /// Return all available messages or an empty array if there are no messages. Empties the queue.
        /// </summary>
        /// <returns></returns>
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
        /// Returns true if there are available messsages.
        /// The out parameter is set to an array of all available messages or null if there are no messages. Empties the queue.
        /// </summary>
        public bool TryDequeueAll(out T[] messages) {
            messages = DequeueAll();
            return messages.Length > 0;
        }

        /// <summary>
        /// Returns true if there is an available message.
        /// The next available message is dequeued and returned via the out parameter.
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

        /// <summary>
        /// Creates and returns an array copy of the current contents of the queue.
        /// </summary>
        public IEnumerable<T> PeekAll() {
            lock (lockList) {
                return list.ToArray();
            }
        }

        /// <summary>
        /// If the queue is empty, returns false and a default T.
        /// Otherwise, returns true and the out parameter is set to the next available message.
        /// </summary>
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
