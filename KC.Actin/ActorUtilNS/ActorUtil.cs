using KC.Actin.ActorUtilNS;
using KC.Actin.Logs;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KC.Actin {
    public class ActorUtil {

        public static ActorUtil Empty => new ActorUtil(null) {
        };

        public ActorUtil(Actor_SansType _actor) {
            this.actor = _actor;
            this.Log = new LogDispatcherForActor(new LogDispatcher(), _actor);
            if (_actor != null) {
                this.Log.AddDestination(_actor.ActorLog);
            }
        }

        private Actor_SansType actor; 
        private Stopwatch timeSinceStarted = new Stopwatch();
        private Stack<string> locations = new Stack<string>();

        public LogDispatcherForActor Log { get; set; }

        public string Context => $"{actor?.ActorName}-{actor?.IdString}";

        private DateTimeOffset m_Started;
        public DateTimeOffset Started {
            get {
                return m_Started;
            }
            set {
                timeSinceStarted.Restart();
                m_Started = value;
            }
        }

        public DateTimeOffset Now {
            get {
                return m_Started.AddMilliseconds(timeSinceStarted.ElapsedMilliseconds);
            }
        }

        public void Update(DispatchData data) {
            this.Started = data.Time;
        }

        private TryResultAsync<T> getTryData<T>(string location, Func<Task<T>> tryThis) {
            return new TryResultAsync<T>(this, location, tryThis, null, null, null, LoggingType.RealTime, 0, false, null);
        }

        public TryResultAsync<T> TryAsync<T>(string location, Func<Task<T>> tryThis) {
            return getTryData(location, tryThis);
        }

        public TryResultAsync TryAsync(string location, Func<Task> tryThis) {
            return new TryResultAsync(getTryData(location, async () => {
                await tryThis();
                return 0;
            }));
        }

        public TryResult<T> Try<T>(string location, Func<T> tryThis) {
            return new TryResult<T>(getTryData(location, async () => {
                return await Task.FromResult(tryThis());
            }));
        }

        public TryResult Try(string location, Action tryThis) {
            return new TryResult(getTryData(location, async () => {
                tryThis();
                return await Task.FromResult(0);
            }));
        }

        internal async Task<T> ExecuteTryTask<T>(TryResultAsync<T> task) {
            locations.Push(task._location);
            try {
                var startTimeMs = this.timeSinceStarted.ElapsedMilliseconds;
                try {
                    try {
                        return await task._tryThis();
                    }
                    catch (Exception ex) when (shouldHandleException(ex)) {
                        if (task._doCatch != null) {
                            try {
                                return await task._doCatch(ex);
                            }
                            catch when (task._swallowOnFailure) {
                            }
                        }
                        return default(T);
                    }
                    finally {
                        if (task._doFinally != null) {
                            await task._doFinally();
                        }
                    }

                    bool shouldHandleException(Exception ex) {
                        try {
                            task._filterException(ex);
                        }
                        catch { }

                        if (task._loggingType != LoggingType.None) {
                            Action<string, string, Exception> doLog;
                            switch (task._loggingType) {
                                case LoggingType.RealTime:
                                    doLog = Log.RealTime; break;
                                case LoggingType.Error:
                                    doLog = Log.Error; break;
                                case LoggingType.Info:
                                    doLog = Log.Info; break;
                                default:
                                    doLog = Log.RealTime; break;
                            }
                            try {
                                doLog(task._niceErrorMessage, string.Join("/", locations), ex);
                            }
                            catch { }
                        }

                        return task._doCatch != null || task._swallowOnFailure;
                    }
                }
                finally {
                    var ms = timeSinceStarted.ElapsedMilliseconds - startTimeMs;
                    if (ms > task._skipProfilingMs) {
                        try {
                            //TODO: Profiling. Store on the actor in a dictionary. X lowest, X highest, running mean.
                        }
                        catch { }
                    }
                }
            }
            finally {
                locations.Pop();
            }
        }
    }
    
}
