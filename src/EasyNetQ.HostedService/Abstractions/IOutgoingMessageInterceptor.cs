using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EasyNetQ.HostedService.Abstractions
{
    /// <summary>
    /// A message interceptor for outgoing messages.
    ///
    /// Messages are intercepted before they are sent to the RabbitMQ server.
    ///
    /// This mechanism can be used, eg, to inject headers to outgoing messages, in a transparent manner.
    /// </summary>
    public interface IOutgoingMessageInterceptor
    {
        /// <summary>
        /// Intercepts outgoing messages before they are sent to the RabbitMQ server.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        Task InterceptMessage(byte[] message, Type type, IDictionary<string, object> headers,
            CancellationToken cancellationToken);
    }

    /// <inheritdoc cref="IOutgoingMessageInterceptor"/>
    /// <typeparam name="T">
    /// The subclass of <see cref="RabbitMqProducer{T}"/> for which the <see cref="IOutgoingMessageInterceptor{T}"/>
    /// has being registered.
    /// </typeparam>
    /// <remarks>
    /// Type parameter <typeparamref name="T"/> can be used to differentiate between the various
    /// <see cref="IOutgoingMessageInterceptor{T}"/> implementations that have been registered with dependency
    /// injection.
    /// </remarks>
    // ReSharper disable once UnusedTypeParameter
    public interface IOutgoingMessageInterceptor<T> : IOutgoingMessageInterceptor
    {
    }
}
