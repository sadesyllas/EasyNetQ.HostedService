using System;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Models
{
    /// <summary>
    /// <inheritdoc cref="IRabbitMqConfig"/>
    /// </summary>
    [Serializable]
    public sealed class RabbitMqConfig : IRabbitMqConfig, ICloneable
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public string Id { get; set; } = "Anonymous";

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public string HostName { get; set; } = "localhost";

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public ushort Port { get; set; } = 5672;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public string UserName { get; set; } = "guest";

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public TimeSpan RequestedHeartbeat { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public TimeSpan ReconnectionAttemptInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <remarks>
        /// By default, it is set to <c>true</c>.
        /// </remarks>
        public bool PersistentMessages { get; set; } = true;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public bool PublisherConfirms { get; set; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public TimeSpan MessageDeliveryTimeout { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public int PublisherLoopErrorBackOffMilliseconds { get; set; } = 100;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public IRabbitMqConfig Copy => (RabbitMqConfig) Clone();

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <returns/>
        public object Clone() =>
            new RabbitMqConfig
            {
                Id = Id,
                HostName = HostName,
                MessageDeliveryTimeout = new TimeSpan(MessageDeliveryTimeout.Ticks),
                Password = Password,
                PersistentMessages = PersistentMessages,
                Port = Port,
                PublisherConfirms = PublisherConfirms,
                PublisherLoopErrorBackOffMilliseconds = PublisherLoopErrorBackOffMilliseconds,
                ReconnectionAttemptInterval = new TimeSpan(ReconnectionAttemptInterval.Ticks),
                RequestedHeartbeat = new TimeSpan(RequestedHeartbeat.Ticks),
                UserName = UserName,
                VirtualHost = VirtualHost
            };
    }
}
