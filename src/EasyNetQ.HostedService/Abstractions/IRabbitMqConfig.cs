using EasyNetQ.HostedService.Models;
using EasyNetQ.Topology;

namespace EasyNetQ.HostedService.Abstractions
{
    /// <summary>
    /// The RabbitMQ related configuration section which contains all the necessary details for connecting to the
    /// RabbitMQ server.
    /// </summary>
    public interface IRabbitMqConfig
    {
        /// <summary>
        /// Provides a way to name a RabbitMQ configuration in order to provide connection reusability.
        ///
        /// When the same <see cref="Id"/> is used for multiple consumers and/or producers, then all of them will use
        /// the same <see cref="IAdvancedBus"/> and thus, the same connection to the RabbitMQ server.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The RabbitMQ server hostname to connect to.
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// The RabbitMQ server port to connect to.
        /// </summary>
        public ushort Port { get; set; }

        /// <summary>
        /// The RabbitMQ virtual host to use when connecting to the RabbitMQ server.
        /// </summary>
        public string VirtualHost { get; set; }

        /// <summary>
        /// The username with which to connect to RabbitMQ.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// The password with which to connect to RabbitMQ.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// The connection heartbeat in seconds.
        /// </summary>
        public ushort RequestedHeartbeatSeconds { get; set; }

        /// <summary>
        /// For producers, whether to use persistent messages when sending a message.
        /// </summary>
        public bool PersistentMessages { get; set; }

        /// <summary>
        /// For producers, whether to use RabbitMQ's Publisher Confirms feature.
        /// </summary>
        public bool PublisherConfirms { get; set; }

        /// <summary>
        /// For producers, it sets the timeout for publishing a message to the RabbitMQ server.
        /// </summary>
        public ushort MessageDeliveryTimeoutSeconds { get; set; }

        /// <summary>
        /// For the default producer implementation, the back off delay when an error occurs in the producer's loop.
        ///
        /// For details about the producer's queue, see <see cref="RabbitMqProducer{T}"/>.
        /// </summary>
        public int PublisherLoopErrorBackOffMilliseconds { get; set; }

        /// <summary>
        /// The queue configuration to use for consumers.
        ///
        /// For details about consumer queue configuration, see <see cref="QueueConfig"/>.
        /// </summary>
        public QueueConfig Queue { get; set; }

        /// <summary>
        /// The <see cref="IQueue"/> returned when a queue is declared by a consumer.
        /// </summary>
        public IQueue? DeclaredQueue { get; set; }
    }
}
