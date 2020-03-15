using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using EasyNetQ.HostedService.Abstractions;
using EasyNetQ.Topology;

namespace EasyNetQ.HostedService.Models
{
    /// <summary>
    /// <inheritdoc cref="IRabbitMqConfig"/>
    /// </summary>
    [Serializable]
    public sealed class RabbitMqConfig : IRabbitMqConfig, ICloneable
    {
        private IQueue? _declaredQueue;

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
        public ushort RequestedHeartbeatSeconds { get; set; } = 60;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public QueueConfig? Queue { get; set; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public IQueue? DeclaredQueue
        {
            get => _declaredQueue;
            set
            {
                if (value == null)
                {
                    throw new Exception($"Null is not a valid value when setting {nameof(DeclaredQueue)}.");
                }

                Queue = new QueueConfig
                {
                    Name = value.Name,
                    Durable = value.IsDurable,
                    DeclareExclusive = value.IsExclusive,
                    AutoDelete = value.IsAutoDelete
                };

                _declaredQueue = value;
            }
        }

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
        public ushort MessageDeliveryTimeoutSeconds { get; set; } = 1;

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
        public object Clone()
        {
            using var memoryStream = new MemoryStream();

            var formatter = new BinaryFormatter();

            formatter.Serialize(memoryStream, this);

            memoryStream.Position = 0;

            return formatter.Deserialize(memoryStream);
        }
    }
}
