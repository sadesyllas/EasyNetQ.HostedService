using System.Threading;
using System.Threading.Tasks;

namespace EasyNetQ.HostedService.Message.Abstractions
{
    /// <summary>
    /// A message handler which knows how to handle messages of type <c>TMessage</c>.
    /// </summary>
    /// <typeparam name="TMessage"/>
    public interface IMessageHandler<in TMessage>
    {
        /// <summary>
        /// The message handling function that an <see cref="IMessageHandler{TMessage}"/> uses to handle messages of
        /// type <see cref="IMessage{TMessage}"/>.
        /// </summary>
        /// <param name="m"/>
        /// <param name="i"/>
        /// <param name="t"/>
        /// <returns/>
        Task HandleMessage(IMessage<TMessage> m, MessageReceivedInfo i, CancellationToken t);
    }
}
