using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace KC.Actin.Logs {
    public class LogDispatcher : IActinLogger {

        internal readonly ActinClock Clock;
        public LogDispatcher(ActinClock clock) {
            if (clock == null) {
                throw new ArgumentNullException("clock may not be null. This is typically the Director.Time object");
            }
            this.Clock = clock; 
        }

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
            log = log.WithNoNulls();
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

        private void dispatch(string context, string location, string userMessage, Exception ex, LogType logType) {
            dispatch(context, location, userMessage, ex?.ToString(), logType);
        }

        private void dispatch(string context, string location, string userMessage, string details, LogType logType) {
            var log = new ActinLog(Clock.Now, context, location, userMessage, details, logType);
            Log(log);
        }

        public void Info(string location) {
            dispatch(null, location, null, (string)null, LogType.Info);
        }

        public void Info(string location, string context) {
            dispatch(context, location, null, (string)null, LogType.Info);
        }

        public void Info(string location, string context, string userMessage, Exception ex) {
            dispatch(context, location, userMessage, ex, LogType.Info);
        }

        public void Info(string location, string context, string userMessage, string details = null) {
            dispatch(context, location, userMessage, details, LogType.Info);
        }

        public void Error(string location) {
            dispatch(null, location, null, (string)null, LogType.Error);
        }

        public void Error(string location, Exception ex) {
            dispatch(null, location, null, ex, LogType.Error);
        }

        public void Error(string location, string context, Exception ex) {
            dispatch(context, location, null, ex, LogType.Error);
        }

        public void Error(string location, string context, string userMessage, Exception ex) {
            dispatch(context, location, userMessage, ex, LogType.Error);
        }

        public void Error(string location, string context, string userMessage, string details = null) {
            dispatch(context, location, userMessage, details, LogType.Error);
        }

        public void RealTime(string location) {
            dispatch(null, location, null, (string)null, LogType.RealTime);
        }

        public void RealTime(string location, string context) {
            dispatch(context, location, null, (string)null, LogType.RealTime);
        }

        public void RealTime(string location, string context, string userMessage) {
            dispatch(context, location, userMessage, (string)null, LogType.RealTime);
        }

        public void RealTime(string location, Exception ex) {
            dispatch(null, location, null, ex, LogType.RealTime);
        }

        public void RealTime(string location, string context, Exception ex) {
            dispatch(context, location, null, ex, LogType.RealTime);
        }

        public void RealTime(string location, string context, string userMessage, Exception ex) {
            dispatch(context, location, userMessage, ex, LogType.RealTime);
        }

        public void RealTime(string location, string context, string userMessage, string details = null) {
            dispatch(context, location, userMessage, details, LogType.RealTime);
        }
    }
}
