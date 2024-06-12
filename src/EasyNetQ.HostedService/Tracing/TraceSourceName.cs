using System.Diagnostics;

namespace EasyNetQ.HostedService.Tracing
{
    /// <summary>
    /// Holds the keys for creating instances of <see cref="DiagnosticSource"/> and <see cref="ActivitySource"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the derived <see cref="RabbitMqService{T}"/>, which implements a consumer or a producer.
    /// </typeparam>
    public static class TraceSourceName<T>
    {
        /// <summary>
        /// The key for a <see cref="DiagnosticSource"/>, through which logs are emitted.
        /// </summary>
        public static readonly string Log = $"{TraceSourceName.KeyPrefix}{typeof(T).FullName}.Log";

        /// <summary>
        /// The key for an <see cref="ActivitySource"/>, through which consumer and producers emit events on receiving
        /// and publishing a message, respectively.
        /// </summary>
        public static readonly string Activity = $"{TraceSourceName.KeyPrefix}{typeof(T).FullName}.Activity";
    }

    /// <summary>
    /// Holds the key prefix for all keys in <see cref="TraceSourceName{T}"/>.
    /// </summary>
    public static class TraceSourceName
    {
        /// <summary>
        /// The key prefix for all keys in <see cref="TraceSourceName{T}"/>.
        /// </summary>
        public static readonly string KeyPrefix =
            $"{typeof(TraceSourceName).Assembly.GetName().Name}@{typeof(TraceSourceName).Assembly.GetName().Version}:";
    }
}
