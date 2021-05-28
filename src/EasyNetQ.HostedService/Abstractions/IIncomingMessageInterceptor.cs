using System.Threading;
using System.Threading.Tasks;

namespace EasyNetQ.HostedService.Abstractions
{
    /// <summary>
    /// A message interceptor for incoming messages.
    ///
    /// Messages are intercepted before they reach the registered message handler.
    ///
    /// This mechanism can be used, eg, to read headers in incoming messages, in a transparent manner.
    /// </summary>
    public interface IIncomingMessageInterceptor
    {
        /// <summary>
        /// Intercepts incoming messages before they reach the registered message handler.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="messageReceivedInfo"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T">
        /// The type of the received message.
        /// </typeparam>
        Task InterceptMessage<T>(IMessage<T> message, MessageReceivedInfo messageReceivedInfo,
            CancellationToken cancellationToken);
    }

    /// <inheritdoc cref="IIncomingMessageInterceptor"/>
    /// <typeparam name="T">
    /// The subclass of <see cref="RabbitMqConsumer{T}"/> for which the <see cref="IIncomingMessageInterceptor{T}"/>
    /// has being registered.
    /// </typeparam>
    /// <remarks>
    /// Type parameter <typeparamref name="T"/> can be used to differentiate between the various
    /// <see cref="IIncomingMessageInterceptor{T}"/> implementations that have been registered with dependency
    /// injection.
    /// </remarks>
    // ReSharper disable once UnusedTypeParameter
    public interface IIncomingMessageInterceptor<T> : IIncomingMessageInterceptor
    {
    }
}
