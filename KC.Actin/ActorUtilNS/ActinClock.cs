using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin {
    public class ActinClock {
        object lockTime = new object();
        DateTimeOffset? timeAdjustmentStarted;
        DateTimeOffset? simulatedStartTime;
        double? m_TimeMultiplier;
        bool m_StopSimulationAtPresent;

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

        public DateTimeOffset Now => simulatedNowFromTime(DateTimeOffset.Now);

        public void ResetSimulation() {
            lock (lockTime) {
                this.timeAdjustmentStarted = null;
                this.simulatedStartTime = null;
                this.m_TimeMultiplier = null;
            }
        }

        /// <summary>
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
