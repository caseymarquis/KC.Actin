using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin.ActorUtilNS {
    internal enum LoggingType {
        RealTime,
        Info,
        Error,
        None
    }

    interface ITryResult<TSelf, TTry, TCatch, TFinally> {
        //Add new functions here to ensure they're implemented everywhere:
        TSelf Catch(TCatch doCatch);
        TSelf Finally(TFinally doFinally);
        TSelf ErrorMessage(string niceErrorMessage);
        TSelf LogExceptionAs_Error();
        TSelf LogExceptionAs_None();
        TSelf SkipProfiling();
        TSelf SkipProfilingIf(int fasterThanXMilliseconds);
        TSelf SwallowExceptionWithoutCatch();
        TSelf FilterException(Action<Exception> filterException);
    }

    public struct TryResult : ITryResult<TryResult, Action, Action<Exception>, Action> {
        private TryResultAsync<int> data;

        internal TryResult(TryResultAsync<int> data) {
            this.data = data;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public TryResult Catch(Action<Exception> doCatch) {
            return new TryResult(data.Catch(async ex => { doCatch(ex); return 0; }));
        }
        public TryResult ErrorMessage(string niceErrorMessage) {
            return new TryResult(data.ErrorMessage(niceErrorMessage));
        }
        public TryResult FilterException(Action<Exception> filterException) {
            return new TryResult(data.FilterException(filterException));
        }
        public TryResult Finally(Action doFinally) {
            return new TryResult(data.Finally(async () => doFinally()));
        }
        public TryResult LogExceptionAs_Error() {
            return new TryResult(data.LogExceptionAs_Error());
        }
        public TryResult LogExceptionAs_None() {
            return new TryResult(data.LogExceptionAs_None());
        }
        public TryResult SkipProfiling() {
            return new TryResult(data.SkipProfiling());
        }
        public TryResult SkipProfilingIf(int fasterThanXMilliseconds) {
            return new TryResult(data.SkipProfilingIf(fasterThanXMilliseconds));
        }
        public TryResult SwallowExceptionWithoutCatch() {
            return new TryResult(data.SwallowExceptionWithoutCatch());
        }
        public void Execute() {
            this.data._util.ExecuteTryTask(this.data).Wait();
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }

    public struct TryResult<T> : ITryResult<TryResult<T>, Func<T>, Func<Exception, T>, Action> {
        private TryResultAsync<T> data;

        internal TryResult(TryResultAsync<T> data) {
            this.data = data;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public TryResult<T> Catch(Func<Exception, T> doCatch) {
            return new TryResult<T>(data.Catch(async ex => doCatch(ex)));
        }
        public TryResult<T> ErrorMessage(string niceErrorMessage) {
            return new TryResult<T>(data.ErrorMessage(niceErrorMessage));
        }
        public TryResult<T> FilterException(Action<Exception> filterException) {
            return new TryResult<T>(data.FilterException(filterException));
        }
        public TryResult<T> Finally(Action doFinally) {
            return new TryResult<T>(data.Finally(async () => doFinally()));
        }
        public TryResult<T> LogExceptionAs_Error() {
            return new TryResult<T>(data.LogExceptionAs_Error());
        }
        public TryResult<T> LogExceptionAs_None() {
            return new TryResult<T>(data.LogExceptionAs_None());
        }
        public TryResult<T> SkipProfiling() {
            return new TryResult<T>(data.SkipProfiling());
        }
        public TryResult<T> SkipProfilingIf(int fasterThanXMilliseconds) {
            return new TryResult<T>(data.SkipProfilingIf(fasterThanXMilliseconds));
        }
        public TryResult<T> SwallowExceptionWithoutCatch() {
            return new TryResult<T>(data.SwallowExceptionWithoutCatch());
        }
        public T Execute() {
            return data._util.ExecuteTryTask(this.data).Result;
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }

    public struct TryResultAsync : ITryResult<TryResultAsync, Func<Task>, Func<Exception, Task>, Func<Task>> {
        private TryResultAsync<int> data;

        internal TryResultAsync(TryResultAsync<int> data) {
            this.data = data;
        }

        public TryResultAsync Catch(Func<Exception, Task> doCatch) {
            return new TryResultAsync(data.Catch(async ex => { await doCatch(ex); return 0; }));
        }
        public TryResultAsync ErrorMessage(string niceErrorMessage) {
            return new TryResultAsync(data.ErrorMessage(niceErrorMessage));
        }
        public TryResultAsync FilterException(Action<Exception> filterException) {
            return new TryResultAsync(data.FilterException(filterException));
        }
        public TryResultAsync Finally(Func<Task> doFinally) {
            return new TryResultAsync(data.Finally(async () => { await doFinally(); }));
        }
        public TryResultAsync LogExceptionAs_Error() {
            return new TryResultAsync(data.LogExceptionAs_Error());
        }
        public TryResultAsync LogExceptionAs_None() {
            return new TryResultAsync(data.LogExceptionAs_None());
        }
        public TryResultAsync SkipProfiling() {
            return new TryResultAsync(data.SkipProfiling());
        }
        public TryResultAsync SkipProfilingIf(int fasterThanXMilliseconds) {
            return new TryResultAsync(data.SkipProfilingIf(fasterThanXMilliseconds));
        }
        public TryResultAsync SwallowExceptionWithoutCatch() {
            return new TryResultAsync(data.SwallowExceptionWithoutCatch());
        }
        public async Task Execute() {
            await data._util.ExecuteTryTask(this.data);
        }
    }

    public struct TryResultAsync<T> : ITryResult<TryResultAsync<T>, Func<Task<T>>, Func<Exception, Task<T>>, Func<Task>> {
        internal ActorUtil _util;
        internal string _location;
        internal string _niceErrorMessage;
        internal Func<Task<T>> _tryThis;
        internal Func<Exception, Task<T>> _doCatch;
        internal Func<Task> _doFinally;
        internal LoggingType _loggingType;
        internal int _skipProfilingMs;
        internal bool _swallowOnFailure;
        internal Action<Exception> _filterException;

        internal TryResultAsync(ActorUtil util, string location, Func<Task<T>> tryThis, Func<Exception, Task<T>> doCatch, Func<Task> doFinally, string niceErrorMessage, LoggingType loggingType, int skipProfilingIfFasterThanXMs, bool swallowOnFailure, Action<Exception> filterException) {
            _util = util;
            _location = location;
            _tryThis = tryThis;
            _doCatch = doCatch;
            _doFinally = doFinally;
            _loggingType = loggingType;
            _skipProfilingMs = skipProfilingIfFasterThanXMs;
            _niceErrorMessage = niceErrorMessage;
            _swallowOnFailure = swallowOnFailure;
            _filterException = filterException;
        }

        public TryResultAsync<T> Catch(Func<Exception, Task<T>> doCatch) {
            return new TryResultAsync<T>(_util, _location, _tryThis, doCatch, _doFinally, _niceErrorMessage, _loggingType, _skipProfilingMs, _swallowOnFailure, _filterException);
        }

        public TryResultAsync<T> Finally(Func<Task> doFinally) {
            return new TryResultAsync<T>(_util, _location, _tryThis, _doCatch, doFinally, _niceErrorMessage, _loggingType, _skipProfilingMs, _swallowOnFailure, _filterException);
        }

        public TryResultAsync<T> ErrorMessage(string niceErrorMessage) {
            return new TryResultAsync<T>(_util, _location, _tryThis, _doCatch, _doFinally, niceErrorMessage, _loggingType, _skipProfilingMs, _swallowOnFailure, _filterException);
        }

        public TryResultAsync<T> LogExceptionAs_Error() {
            return new TryResultAsync<T>(_util, _location, _tryThis, _doCatch, _doFinally, _niceErrorMessage, LoggingType.Error, _skipProfilingMs, _swallowOnFailure, _filterException);
        }

        public TryResultAsync<T> LogExceptionAs_None() {
            return new TryResultAsync<T>(_util, _location, _tryThis, _doCatch, _doFinally, _niceErrorMessage, LoggingType.None, _skipProfilingMs, _swallowOnFailure, _filterException);
        }

        public TryResultAsync<T> SkipProfiling() {
            return new TryResultAsync<T>(_util, _location, _tryThis, _doCatch, _doFinally, _niceErrorMessage, _loggingType, int.MaxValue, _swallowOnFailure, _filterException);
        }

        public TryResultAsync<T> SkipProfilingIf(int fasterThanXMilliseconds) {
            return new TryResultAsync<T>(_util, _location, _tryThis, _doCatch, _doFinally, _niceErrorMessage, _loggingType, fasterThanXMilliseconds, _swallowOnFailure, _filterException);
        }

        public TryResultAsync<T> SwallowExceptionWithoutCatch() {
            return new TryResultAsync<T>(_util, _location, _tryThis, _doCatch, _doFinally, _niceErrorMessage, _loggingType, _skipProfilingMs, true, _filterException);
        }

        public TryResultAsync<T> FilterException(Action<Exception> filterException) {
            return new TryResultAsync<T>(_util, _location, _tryThis, _doCatch, _doFinally, _niceErrorMessage, _loggingType, _skipProfilingMs, _swallowOnFailure, filterException);
        }

        public async Task<T> Execute() {
            return await _util.ExecuteTryTask(this);
        }
    }

}
