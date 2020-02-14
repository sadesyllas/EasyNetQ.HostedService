using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.HostedService.Internals;
using EasyNetQ.HostedService.Message.Abstractions;

namespace EasyNetQ.HostedService.MessageHandlers.Impl
{
    /// <summary>
    /// <inheritdoc cref="IMessageHandler{TMessage}"/>
    /// </summary>
    internal class MessageHandlersImpl : IMessageHandler<object>
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="message"/>
        /// <param name="i"/>
        /// <param name="t"/>
        /// <returns/>
        /// <exception cref="UnhandledMessageTypeException"/>
        /// <remarks>
        /// This message handler implementation is meant to be used a fallback consumer message handler and that is why
        /// it is implemented for type <see cref="object"/>.
        /// </remarks>
        Task IMessageHandler<object>.HandleMessage(IMessage<object> message, MessageReceivedInfo i, CancellationToken t)
        {
            throw new UnhandledMessageTypeException();
        }
    }
}
