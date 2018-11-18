using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KC.NanoProcesses
{
    public abstract class NanoQueue<T> : NanoProcess
    {
        private object lockList = new object();
        private List<T> list = new List<T>();
        private Queue<T> processing = new Queue<T>();

        private object lockMaxItems = new object();
        private int m_MaxItems = int.MaxValue;
        public int MaxItems {
            get {
                lock (lockMaxItems) {
                    return m_MaxItems;
                }
            }
            set {
                m_MaxItems = value;
            }
        }

        abstract protected Task<Queue<T>> OnRun(NpUtil util, Queue<T> items);

        public void Enqueue(T item) {
            lock (lockList) {
                list.Add(item);
            }
        }

        public void EnqueueRange(IEnumerable<T> items) {
            lock (lockList) {
                list.AddRange(items);
            }
        }

        public int Count {
            get {
                lock (lockList) {
                    return list.Count;
                }
            }
        }

        public override string ProcessName => throw new NotImplementedException();

        protected override TimeSpan RunDelay => throw new NotImplementedException();

        protected async override Task OnRun(NpUtil util) {
            lock (lockList) {
                foreach (var item in list) {
                    this.processing.Enqueue(item);
                }
            }
            try {
                var gotBack = await this.OnRun(util, this.processing);
                if (gotBack != null) {
                    this.processing = gotBack;
                }
            }
            finally {
                lock (lockList) {
                    this.list.InsertRange(0, this.processing);
                    this.processing.Clear();
                }
            }
        }

    }
}
