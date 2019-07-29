using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace KC.Actin.Logs {
    public class LogDispatcher : IActinLogger {
        private ReaderWriterLockSlim lockDestinations = new ReaderWriterLockSlim();
        private List<IActinLogger> destinations = new List<IActinLogger>();

        public void AddDestination(IActinLogger destination) {
            if (destination == null || destination == this) {
                return;
            }
            lockDestinations.EnterWriteLock();
            try {
                destinations.Add(destination);
            }
            finally {
                lockDestinations.ExitWriteLock();
            }
        }

        public void RemoveDestination(IActinLogger destination) {
            lockDestinations.EnterWriteLock();
            try {
                destinations.Remove(destination);
            }
            finally {
                lockDestinations.ExitWriteLock();
            }
        }

        public void Log(ActinLog log) {
            lockDestinations.EnterReadLock();
            try {
                foreach (var destination in destinations) {
                    destination.Log(log);
                }
            }
            finally {
                lockDestinations.ExitReadLock();
            }
        }

        private void dispatch(string context, string location, string userMessage, Exception ex, string type) {
            dispatch(context, location, userMessage, ex?.ToString(), type);
        }

        private void dispatch(string context, string location, string userMessage, string details, string type) {
            var log = new ActinLog(DateTimeOffset.Now, context, location, userMessage, details, type);
            Log(log);
        }

        public void Info(string location) {
            dispatch(null, location, null, (string)null, "Info");
        }

        public void Info(string location, string context) {
            dispatch(context, location, null, (string)null, "Info");
        }

        public void Info(string location, string context, string userMessage, Exception ex) {
            dispatch(context, location, userMessage, ex, "Info");
        }

        public void Info(string location, string context, string userMessage, string details = null) {
            dispatch(context, location, userMessage, details, "Info");
        }

        public void Error(string location) {
            dispatch(null, location, null, (string)null, "Error");
        }

        public void Error(string location, Exception ex) {
            dispatch(null, location, null, ex, "Error");
        }

        public void Error(string location, string context, Exception ex) {
            dispatch(context, location, null, ex, "Error");
        }

        public void Error(string location, string context, string userMessage, Exception ex) {
            dispatch(context, location, userMessage, ex, "Error");
        }

        public void Error(string location, string context, string userMessage, string details = null) {
            dispatch(context, location, userMessage, details, "Error");
        }

        public void RealTime(string location) {
            dispatch(null, location, null, (string)null, "RealTime");
        }

        public void RealTime(string location, string context) {
            dispatch(context, location, null, (string)null, "RealTime");
        }

        public void RealTime(string location, string context, string userMessage) {
            dispatch(context, location, userMessage, (string)null, "RealTime");
        }

        public void RealTime(string location, Exception ex) {
            dispatch(null, location, null, ex, "RealTime");
        }

        public void RealTime(string location, string context, Exception ex) {
            dispatch(context, location, null, ex, "RealTime");
        }

        public void RealTime(string location, string context, string userMessage, Exception ex) {
            dispatch(context, location, userMessage, ex, "RealTime");
        }

        public void RealTime(string location, string context, string userMessage, string details = null) {
            dispatch(context, location, userMessage, details, "RealTime");
        }
    }
}
