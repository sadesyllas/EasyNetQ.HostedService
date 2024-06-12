using System.Diagnostics;

namespace EasyNetQ.HostedService.Tracing
{
    /// <summary>
    /// The keys that describe the activity tag names being emitted through instances of <see cref="ActivitySource"/>.
    /// </summary>
    public static class TraceActivityTagName
    {
        /// <summary>
        /// The tag name for the exchange where a RabbitMQ message is being published.
        /// </summary>
        public static readonly string Exchange = "Exchange";

        /// <summary>
        /// The tag name for the rouging key used for a RabbitMQ message that is being published.
        /// </summary>
        public static readonly string RoutingKey = "RoutingKey";

        /// <summary>
        /// The tag name for the <c>mandatory</c> boolean value, used when a RabbitMQ message is being published.
        /// </summary>
        public static readonly string Mandatory = "Mandatory";

        /// <summary>
        /// The tag name for the headers of a RabbitMQ message that is being consumed or published.
        /// </summary>
        public static readonly string Headers = "Headers";

        /// <summary>
        /// The tag name for the correlation id of a RabbitMQ message that is being consumed.
        /// </summary>
        public static readonly string CorrelationId = "CorrelationId";

        /// <summary>
        /// The tag name for the delivery tag of a RabbitMQ message that is being consumed.
        /// </summary>
        public static readonly string DeliveryTag = "DeliveryTag";

        /// <summary>
        /// The tag name for the <c>redelivered</c> flag of a RabbitMQ message that is being consumed.
        /// </summary>
        public static readonly string Redelivered = "Redelivered";

        /// <summary>
        /// The tag name used when an exception occurs in an <see cref="Activity"/>, in a
        /// <see cref="RabbitMqProducer{T}"/>.
        /// </summary>
        public static readonly string Exception = "Exception";
    }
}
