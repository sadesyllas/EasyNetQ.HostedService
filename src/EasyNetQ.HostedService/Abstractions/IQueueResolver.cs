using System;
using EasyNetQ.HostedService.Models;

namespace EasyNetQ.HostedService.Abstractions
{
    /// <summary>
    /// An instance of <see cref="IQueueResolver"/> is responsible for providing a queue name for a consumer to
    /// use.
    /// </summary>
    /// <remarks>
    /// The <see cref="IServiceProvider"/> provided to <see cref="RabbitMqService{T}"/> will be used to retrieve a
    /// registered <see cref="IQueueResolver"/> instance if the <see cref="RabbitMqService{T}"/> is a consumer
    /// and the provided <see cref="IRabbitMqConfig.Queue"/> is <c>null</c>.
    /// </remarks>
    public interface IQueueResolver
    {
        /// <summary>
        /// The queue name to be used by a consumer.
        /// </summary>
        QueueConfig Queue { get; }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <remarks>
    /// This is meant to be used to register an <see cref="IQueueResolver"/> with dependency injection.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    // ReSharper disable once UnusedTypeParameter
    public interface IQueueResolver<T> : IQueueResolver
    {
    }
}
