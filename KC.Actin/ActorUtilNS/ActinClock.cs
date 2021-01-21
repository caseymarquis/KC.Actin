using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin {
    public class ActinClock {
        object lockTime = new object();
        DateTimeOffset? timeAdjustmentStarted;
        DateTimeOffset? simulatedStartTime;
        double? m_TimeMultiplier;

        public bool InSimulation {
            get {
                lock (lockTime) {
                    return timeAdjustmentStarted.HasValue;
                }
            }
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

        DateTimeOffset simulatedNowFromTime(DateTimeOffset now) {
            lock (lockTime) {
                if (timeAdjustmentStarted == null) {
                    return now;
                }
                var multiplier = m_TimeMultiplier ?? 1;
                var adjustmentStarted = timeAdjustmentStarted.Value;
                var simulationStart = simulatedStartTime ?? timeAdjustmentStarted.Value;

                var totalSimulatedTicks = (now - adjustmentStarted).Ticks * multiplier;
                return simulationStart.AddTicks((long)totalSimulatedTicks);
            }
        }
    }
}
