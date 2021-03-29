using System;
using EasyNetQ.HostedService.Models;

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
        string Id { get; set; }

        /// <summary>
        /// The RabbitMQ server hostname to connect to.
        /// </summary>
        string HostName { get; set; }

        /// <summary>
        /// The RabbitMQ server port to connect to.
        /// </summary>
        ushort Port { get; set; }

        /// <summary>
        /// The RabbitMQ virtual host to use when connecting to the RabbitMQ server.
        /// </summary>
        string VirtualHost { get; set; }

        /// <summary>
        /// The username with which to connect to RabbitMQ.
        /// </summary>
        string UserName { get; set; }

        /// <summary>
        /// The password with which to connect to RabbitMQ.
        /// </summary>
        string Password { get; set; }

        /// <summary>
        /// The connection heartbeat in seconds.
        /// </summary>
        TimeSpan RequestedHeartbeat { get; set; }

        /// <summary>
        /// The interval with which to try to reconnect, once disconnected.
        /// </summary>
        TimeSpan ReconnectionAttemptInterval { get; set; }

        /// <summary>
        /// For producers, whether to use persistent messages when sending a message.
        /// </summary>
        bool PersistentMessages { get; set; }

        /// <summary>
        /// For producers, whether to use RabbitMQ's Publisher Confirms feature.
        /// </summary>
        bool PublisherConfirms { get; set; }

        /// <summary>
        /// For producers, it sets the timeout for publishing a message to the RabbitMQ server.
        /// </summary>
        TimeSpan MessageDeliveryTimeout { get; set; }

        /// <summary>
        /// For the default producer implementation, the back off delay when an error occurs in the producer's loop.
        ///
        /// For details about the producer's queue, see <see cref="RabbitMqProducer{T}"/>.
        /// </summary>
        int PublisherLoopErrorBackOffMilliseconds { get; set; }

        /// <summary>
        /// Makes easy reusing a <see cref="RabbitMqConfig"/>.
        /// </summary>
        IRabbitMqConfig Copy { get; }
    }
}
