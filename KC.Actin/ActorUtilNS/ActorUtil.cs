using KC.Actin.ActorUtilNS;
using KC.Actin.Logs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace KC.Actin {
    /// <summary>
    /// This class is passed to an Actor when it is Initialized, Run, or Disposed.
    /// It contains functionality for logging, profiling, a helper field
    /// for accessing the current (or simulated) time, and other similar utilities.
    /// </summary>
    public class ActorUtil {
        /// <summary>
        /// Instantiate a new ActorUtil. There should be virtually no need to do this
        /// outside the core library.
        /// </summary>
        public ActorUtil(Actor_SansType _actor, ActinClock clock) {
            if (clock == null) {
                throw new ArgumentNullException("clock may not be null");
            }
            this.clock = clock;
            this.actor = _actor;
            this.Log = new LogDispatcherForActor(new LogDispatcher(clock), _actor);
        }

        //Init only properties would make this nicer, but we'd have to bump up from .net standard 2.1
        readonly Atom<bool> isTestAtom = new Atom<bool>(false);
        /// <summary>
        /// Set to true if an Actor is run using the ActinTest class.
        /// </summary>
        public bool _IsTest_ {
            get {
                return isTestAtom.Value;
            }
            set {
                isTestAtom.Value = value;
            }
        }

        private Actor_SansType actor;
        private ActinClock clock;
        private Stack<string> locations = new Stack<string>();

        /// <summary>
        /// Create logs which are labeled as originating with the Actor.
        /// </summary>
        public LogDispatcherForActor Log { get; set; }

        /// <summary>
        /// The string id and name of the actor associated with this ActorUtil.
        /// </summary>
        public string Context => $"{actor?.ActorName}-{actor?.IdString}";

        /// <summary>
        /// When the actor most recently started running.
        /// </summary>
        public DateTimeOffset Started { get; private set; }

        /// <summary>
        /// The current system time, or the simulated system time if the Director or ActinTest clock has been modified.
        /// </summary>
        public DateTimeOffset Now => actor?.IgnoreSimulatedTime == true ? DateTimeOffset.Now : clock.Now;
        /// <summary>
        /// Returns true if the actor is being fed a simulated system time.
        /// </summary>
        public bool InSimulation => actor?.IgnoreSimulatedTime == true ? false : clock.InSimulation;

        /// <summary>
        /// A token that will be canceled if the actor is disposed.
        /// </summary>
        public CancellationToken ActorDisposedToken => actor.ActorDisposedToken;

        Stopwatch stopWatch = new Stopwatch();

        internal void ResetStartTime() {
            this.Started = this.Now;
            stopWatch.Restart();
        }

    }
    
}
