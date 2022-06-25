using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin {
    /// <summary>
    /// During testing, or when running a simulation, it's often useful to have full
    /// control over the perceived system time. This class allows you to do this.
    /// When actors are run, instead of directly accessing DateTimeOffset.Now, you should
    /// instead use <c cref="ActorUtil.Now">ActorUtil.Now</c>. You can then fully control the perceived
    /// time during testing, or if the need arises for other reasons. The ActinTest class and the Director
    /// class both have a public Clock which allow for direct control over the time passed to actors.
    /// </summary>
    public class ActinClock {
        object lockTime = new object();
        DateTimeOffset? timeAdjustmentStarted;
        DateTimeOffset? simulatedStartTime;
        double? m_TimeMultiplier;
        bool m_StopSimulationAtPresent;

        /// <summary>
        /// Returns true if the clock will return a fake time.
        /// </summary>
        public bool InSimulation {
            get {
                lock (lockTime) {
                    return timeAdjustmentStarted.HasValue;
                }
            }
        }

        /// <summary>
        /// Off by default. When turned on, time simulation will automatically turn itself off when
        /// the simulation has reached the present time. This allows a smooth transition from speeding
        /// through past time to operating normally in the present. This is useful when simulating
        /// past data at high speed, and then transitioning into a normal simulation when finished.
        /// </summary>
        public bool StopSimulationAtPresent {
            get { lock (lockTime) return m_StopSimulationAtPresent; }
            set { lock (lockTime) m_StopSimulationAtPresent = value; }
        }

        /// <summary>
        /// The current time, or the current simulated time.
        /// </summary>
        public DateTimeOffset Now => simulatedNowFromTime(DateTimeOffset.Now);

        /// <summary>
        /// Disable time simulation and instead return the accurate system time.
        /// </summary>
        public void ResetSimulation() {
            lock (lockTime) {
                this.timeAdjustmentStarted = null;
                this.simulatedStartTime = null;
                this.m_TimeMultiplier = null;
            }
        }

        /// <summary>
        /// Simulate a specific time, and set the speed that time will progress.
        /// If the timeMultiplier is set to 0, then time will not progress until
        /// this function is called again.
        /// Once time simulation has started, you can turn it off by using ResetSimulation.
        /// All calls to this function assume you want to continue faking the time.
        /// Setting an argument to null means "Don't change this on me". It doesn't mean reset
        /// the value. 
        /// </summary>
        public void Simulate(DateTimeOffset? setTimeTo, double? timeMultiplier) {
            lock (lockTime) {
                var now = DateTimeOffset.Now;
                var currentSimulatedTime = simulatedNowFromTime(now); //If there's no simulation, will return 'now'
                timeAdjustmentStarted = now;
                simulatedStartTime = setTimeTo ?? currentSimulatedTime;
                m_TimeMultiplier = timeMultiplier ?? m_TimeMultiplier;
            }
        }

        DateTimeOffset simulatedNowFromTime(DateTimeOffset systemNow) {
            lock (lockTime) {
                if (timeAdjustmentStarted == null) {
                    return systemNow;
                }
                var multiplier = m_TimeMultiplier ?? 1;
                var adjustmentStarted = timeAdjustmentStarted.Value;
                var simulationStart = simulatedStartTime ?? timeAdjustmentStarted.Value;

                var totalSimulatedTicks = (systemNow - adjustmentStarted).Ticks * multiplier;
                var simulatedNow = simulationStart.AddTicks((long)totalSimulatedTicks);

                if (m_StopSimulationAtPresent) {
                    if (simulatedNow >= systemNow) {
                        ResetSimulation();
                        return systemNow;
                    }
                }

                return simulatedNow;
            }
        }
    }
}
