using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin.Logs {
    public class LogDispatcherForActor {
        private LogDispatcher dispatcher;
        private Actor_SansType actor;
        public LogDispatcherForActor(LogDispatcher dispatcher, Actor_SansType actor) {
            this.dispatcher = dispatcher;
            this.actor = actor;
        }

        public void AddDestination(IActinLogger destination) {
            dispatcher.AddDestination(destination);
        }

        public void RemoveDestination(IActinLogger destination) {
            dispatcher.RemoveDestination(destination);
        }

        public void Log(ActinLog log) {
            this.dispatcher.Log(log);
        }

        private void dispatch(string userMessage, string secondaryLocation, Exception ex, string type) {
            dispatch(userMessage, secondaryLocation, ex?.ToString(), type);
        }

        private void dispatch(string userMessage, string secondaryLocation, string details, string type) {
            var location = secondaryLocation == null ? actor?.ActorName : $"{actor?.ActorName}.{secondaryLocation}";
            var log = new ActinLog(DateTimeOffset.Now, actor?.IdString, actor.ActorName, userMessage, details, type);
            Log(log);
        }

        public void Info(string userMessage) {
            dispatch(userMessage,(string)null, (string)null, "Info");
        }

        public void Info(string userMessage, string location) {
            dispatch(userMessage, location, (string)null, "Info");
        }

        public void Info(string userMessage, string location, Exception ex) {
            dispatch(userMessage, location, ex, "Info");
        }

        public void Info(string userMessage, string location, string details) {
            dispatch(location, userMessage, details, "Info");
        }

        public void Error(string userMessage) {
            dispatch(userMessage, null, (string)null, "Error");
        }

        public void Error(string userMessage, string location) {
            dispatch(userMessage, location, (string)null, "Error");
        }

        public void Error(Exception ex) {
            dispatch(null, null, ex, "Error");
        }

        public void Error(string userMessage, Exception ex) {
            dispatch(userMessage, null, ex, "Error");
        }

        public void Error(string userMessage, string location, Exception ex) {
            dispatch(userMessage, location, ex, "Error");
        }

        public void Error(string userMessage, string location, string details) {
            dispatch(userMessage, location, details, "Error");
        }

        public void RealTime(Exception ex) {
            dispatch(null, null, ex, "RealTime");
        }

        public void RealTime(string userMessage) {
            dispatch(userMessage, null, (string)null, "RealTime");
        }

        public void RealTime(string userMessage, string location) {
            dispatch(userMessage, location, (string) null, "RealTime");
        }

        public void RealTime(string userMessage, Exception ex) {
            dispatch(userMessage, null, ex, "RealTime");
        }

        public void RealTime(string userMessage, string location, Exception ex) {
            dispatch(userMessage, location, ex, "RealTime");
        }

        public void RealTime(string userMessage, string location, string details) {
            dispatch(userMessage, location, details, "RealTime");
        }
    }
}
