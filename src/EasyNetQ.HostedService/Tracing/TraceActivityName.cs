using System.Diagnostics;

namespace EasyNetQ.HostedService.Tracing
{
    /// <summary>
    /// The keys that describe the activity names being emitted through instances of <see cref="ActivitySource"/>.
    /// </summary>
    public static class TraceActivityName
    {
        /// <summary>
        /// The key used when starting a new <see cref="Activity"/> in a <see cref="RabbitMqConsumer{T}"/>.
        /// </summary>
        public static readonly string Consume = "Consume";

        /// <summary>
        /// The key used when starting a new <see cref="Activity"/> in a <see cref="RabbitMqProducer{T}"/>.
        /// </summary>
        public static readonly string Publish = "Publish";
    }
}
