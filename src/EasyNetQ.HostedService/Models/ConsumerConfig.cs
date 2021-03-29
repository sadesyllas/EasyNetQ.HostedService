using EasyNetQ.Topology;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace EasyNetQ.HostedService.Models
{
    /// <summary>
    /// The class that encapsulates the configuration of a consumer.
    ///
    /// It is expected to be provided by classes derived from <see cref="RabbitMqConsumer{T}"/>.
    /// </summary>
    public sealed class ConsumerConfig
    {
        /// <summary>
        /// The <see cref="IQueue"/> object needed by a class derived from <see cref="RabbitMqConsumer{T}"/>, which
        /// represents the queue that the consumer is to consumer from.
        /// </summary>
        public IQueue Queue { get; set; } = null;

        /// <summary>
        /// The consumer's priority.
        /// </summary>
        public int? Priority { get; set; }

        /// <summary>
        /// If true, the consumer is set to be the exclusive consumer of the provided <see cref="Queue"/>.
        /// </summary>
        public bool IsExclusive { get; set; }

        /// <summary>
        /// The prefetch count that will be negotiated on the consumer's channel.
        /// </summary>
        public ushort? PrefetchCount { get; set; }
    }
}
