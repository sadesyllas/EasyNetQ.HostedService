using System;
using EasyNetQ.HostedService.DependencyInjection;
using EasyNetQ.Topology;

namespace EasyNetQ.HostedService.Models
{
    /// <summary>
    /// Queue configuration which is relevant only for consumers.
    /// </summary>
    [Serializable]
    public sealed class QueueConfig
    {
        /// <summary>
        /// The name of the queue from which to consume.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// If set, along with <see cref="RabbitMqServiceBuilder{T}.AutoDeclareQueue"/>, the queue will be declared as
        /// <c>durable</c>.
        /// </summary>
        public bool Durable { get; set; }

        /// <summary>
        /// If set, along with <see cref="RabbitMqServiceBuilder{T}.AutoDeclareQueue"/>, the queue will be declared as
        /// <c>exclusive</c>.
        /// </summary>
        public bool DeclareExclusive { get; set; }

        /// <summary>
        /// If set, along with <see cref="RabbitMqServiceBuilder{T}.AutoDeclareQueue"/>, the queue will be consumed as
        /// <c>exclusive</c>.
        /// </summary>
        public bool ConsumeExclusive { get; set; }

        /// <summary>
        /// If set, along with <see cref="RabbitMqServiceBuilder{T}.AutoDeclareQueue"/>, the queue will be declared as
        /// <c>auto deleted</c>.
        /// </summary>
        public bool AutoDelete { get; set; }

        /// <summary>
        /// If set, the consumer of the queue will have its priority set to the provided value.
        /// </summary>
        public int? Priority { get; set; }

        /// <summary>
        /// If set, the channel through which the queue is being consumed will have the prefetch count set to the
        /// provided value.
        /// </summary>
        public ushort? PrefetchCount { get; set; }

        /// <summary>
        /// A convenience accessor to get the current configuration as a <see cref="IQueue"/>.
        /// </summary>
        public IQueue AsIQueue => new Queue(Name, Durable, DeclareExclusive, AutoDelete);

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <returns/>
        public override string ToString() =>
            $"Name = \"{Name}\", Durable = {Durable}, DeclareExclusive = {DeclareExclusive}, " +
            $"ConsumeExclusive = {ConsumeExclusive}, AutoDelete = {AutoDelete}";
    }
}
