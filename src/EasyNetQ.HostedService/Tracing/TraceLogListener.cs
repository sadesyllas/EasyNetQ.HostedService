using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EasyNetQ.HostedService.Tracing
{
    /// <summary>
    /// A convenience type to help register a handler for log messages, emitted by the library.
    /// </summary>
    public static class TraceLogListener
    {
        /// <summary>
        /// Subscribes an <see cref="Action{TraceLog}"/> as a handler for the log events, emitted
        /// by a <see cref="DiagnosticSource"/>.
        /// </summary>
        /// <param name="handler">
        /// The handler for the log events, emitted by a <see cref="DiagnosticSource"/>.
        /// </param>
        /// <param name="minLogLevel"></param>
        public static IDisposable Subscribe<T>(Action<TraceLog> handler, LogLevel minLogLevel = LogLevel.Debug) =>
            // ReSharper disable once AccessToStaticMemberViaDerivedType
            DiagnosticListener.AllListeners.Subscribe(
                new DiagnosticListenerObserver(listenerName => listenerName == TraceSourceName<T>.Log, handler,
                    minLogLevel));

        /// <summary>
        /// Subscribes an <see cref="Action{TraceLog}"/> as a handler for the log events, emitted
        /// by a <see cref="DiagnosticSource"/>.
        ///
        /// This variant subscribes to all log messages, irrespective from the actual source within the library.
        /// </summary>
        /// <param name="handler">
        /// The handler for the log events, emitted by a <see cref="DiagnosticSource"/>.
        /// </param>
        /// <param name="minLogLevel"></param>
        public static IDisposable SubscribeToAll(Action<TraceLog> handler, LogLevel minLogLevel = LogLevel.Debug) =>
            // ReSharper disable once AccessToStaticMemberViaDerivedType
            DiagnosticListener.AllListeners.Subscribe(
                new DiagnosticListenerObserver(
                    listenerName => listenerName.StartsWith(TraceSourceName.KeyPrefix),
                    handler, minLogLevel));

        private sealed class DiagnosticListenerObserver : IObserver<DiagnosticListener>
        {
            public DiagnosticListenerObserver(Func<string, bool> listenerNamePredicate, Action<TraceLog> handler,
                LogLevel minLogLevel)
            {
                _listenerNamePredicate = listenerNamePredicate;
                _handler = handler;
                _minLogLevel = minLogLevel;
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(DiagnosticListener listener)
            {
                if (_listenerNamePredicate(listener.Name))
                {
                    listener.Subscribe(new LogObserver(_handler, _minLogLevel),
                        (eventName, logLevel, what) =>
                            eventName == TraceEventName.Log && _minLogLevel <= (LogLevel) logLevel);
                }
            }

            private readonly Func<string, bool> _listenerNamePredicate;
            private readonly Action<TraceLog> _handler;
            private readonly LogLevel _minLogLevel;
        }

        private sealed class LogObserver : IObserver<KeyValuePair<string, object>>
        {
            public LogObserver(Action<TraceLog> handler, LogLevel minLogLevel)
            {
                _handler = handler;
                _minLogLevel = minLogLevel;
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(KeyValuePair<string, object> value)
            {
                var traceLog = (TraceLog) value.Value;

                if (_minLogLevel > traceLog.LogLevel)
                {
                    return;
                }

                _handler(traceLog);
            }

            private readonly Action<TraceLog> _handler;
            private readonly LogLevel _minLogLevel;
        }
    }
}
