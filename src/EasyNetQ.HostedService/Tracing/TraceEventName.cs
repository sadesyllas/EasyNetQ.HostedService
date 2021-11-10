using System.Diagnostics;

namespace EasyNetQ.HostedService.Tracing
{
    /// <summary>
    /// The keys that describe what kind of event is being emitted through instances of <see cref="DiagnosticSource"/>
    /// and <see cref="ActivitySource"/>.
    /// </summary>
    public static class TraceEventName
    {
        /// <summary>
        /// The key for all emitted logs, through the emitting <see cref="DiagnosticSource"/>.
        /// </summary>
        public static readonly string Log = "Log";

        /// <summary>
        /// The key used when cancelling an <see cref="Activity"/> in a <see cref="RabbitMqProducer{T}"/>.
        /// </summary>
        public static readonly string Cancelled = "Cancelled";

        /// <summary>
        /// The key used when an exception occurs in an <see cref="Activity"/>, in a <see cref="RabbitMqProducer{T}"/>.
        /// </summary>
        public static readonly string Exception = "Exception";
    }
}
