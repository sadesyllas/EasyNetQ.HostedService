using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace EasyNetQ.HostedService.Tracing
{
    /// <summary>
    /// The class that emits instances of <see cref="TraceLog"/>, through an instance of <see cref="DiagnosticSource"/>.
    /// </summary>
    public sealed class TraceLogWriter
    {
        /// <summary>
        /// Creates a new <see cref="TraceLogWriter"/>.
        /// </summary>
        /// <param name="diagnosticSource"></param>
        public TraceLogWriter(DiagnosticSource diagnosticSource)
        {
            _diagnosticSource = diagnosticSource;
        }

        /// <summary>
        /// Logs a message using the Trace <see cref="LogLevel"/>.
        /// </summary>
        /// <param name="message"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogTrace(string message) => LogTrace(LogLevel.Trace, message);

        /// <summary>
        /// Logs a message using the Debug <see cref="LogLevel"/>.
        /// </summary>
        /// <param name="message"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogDebug(string message) => LogTrace(LogLevel.Debug, message);

        /// <summary>
        /// Logs a message using the Information <see cref="LogLevel"/>.
        /// </summary>
        /// <param name="message"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogInformation(string message) => LogTrace(LogLevel.Information, message);

        /// <summary>
        /// Logs a message using the Error <see cref="LogLevel"/>.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogError(string message, Exception exception = null) =>
            LogTrace(LogLevel.Error, message, exception);

        /// <summary>
        /// Logs a message using the Critical <see cref="LogLevel"/>.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogCritical(string message, Exception exception = null) =>
            LogTrace(LogLevel.Critical, message, exception);

        /// <summary>
        /// This is the core logging method, used by the public one's
        ///
        /// It checks whether there are any listeners for the corresponding <see cref="LogLevel"/> and if there are,
        /// it emits a <see cref="TraceLog"/> instance.
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        private void LogTrace(LogLevel logLevel, string message, Exception exception = null)
        {
            if (_diagnosticSource.IsEnabled(TraceEventName.Log, logLevel))
            {
                _diagnosticSource.Write(TraceEventName.Log, new TraceLog(logLevel, message, exception));
            }
        }

        private readonly DiagnosticSource _diagnosticSource;
    }
}
