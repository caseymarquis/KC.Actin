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
        public ActorUtil(Actor_SansType _actor, ActinClock clock) {
            if (clock == null) {
                throw new ArgumentNullException("clock may not be null");
            }
            this.clock = clock;
            this.actor = _actor;
            this.Log = new LogDispatcherForActor(new LogDispatcher(clock), _actor);
            if (_actor != null) {
                this.Log.AddDestination(_actor.ActorLog);
            }
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

        public LogDispatcherForActor Log { get; set; }

        public string Context => $"{actor?.ActorName}-{actor?.IdString}";

        public DateTimeOffset Started { get; private set; }

        public DateTimeOffset Now => actor?.IgnoreSimulatedTime == true ? DateTimeOffset.Now : clock.Now;
        public bool InSimulation => actor?.IgnoreSimulatedTime == true ? false : clock.InSimulation;

        Stopwatch stopWatch = new Stopwatch();

        public void ResetStartTime() {
            this.Started = this.Now;
            stopWatch.Restart();
        }

    }
    
}
