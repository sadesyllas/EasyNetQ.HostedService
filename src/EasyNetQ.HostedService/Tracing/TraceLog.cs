using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EasyNetQ.HostedService.Tracing
{
    /// <summary>
    /// The type of log emitted from an instance of <see cref="DiagnosticSource"/>.
    /// </summary>
    public sealed class TraceLog
    {
        /// <summary>
        /// Creates a new <see cref="TraceLog"/>.
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        public TraceLog(LogLevel logLevel, string message, Exception exception = null)
        {
            LogLevel = logLevel;
            Message = message;
            Exception = exception;
        }

        /// <summary>
        /// The log's <see cref="LogLevel"/>.
        /// </summary>
        public LogLevel LogLevel { get; }

        /// <summary>
        /// The log's message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The log's <see cref="Exception"/>, if it's an error.
        /// </summary>
        public Exception Exception { get; }
    }
}
