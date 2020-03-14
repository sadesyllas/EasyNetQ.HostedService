using System.Threading;
using System.Threading.Tasks;

namespace EasyNetQ.HostedService.Message.Abstractions
{
    /// <summary>
    /// A handler that handles messages received by a RabbitMQ consumer.
    /// </summary>
    /// <param name="message">
    /// The received message.
    /// </param>
    /// <param name="info">
    /// The RabbitMQ metadata that accompanied the received message.
    /// </param>
    /// <param name="token">
    /// A cancellation token with which the handling of the message can be canceled.
    /// </param>
    /// <returns>
    /// Returns a task, with which client code can wait for a message to be handled.
    /// </returns>
    public delegate Task MessageHandler(IMessage message, MessageReceivedInfo info, CancellationToken token);

    /// <summary>
    /// A handler that handles messages received by a RabbitMQ consumer.
    /// </summary>
    /// <param name="message">
    /// The received message.
    /// </param>
    /// <param name="info">
    /// The RabbitMQ metadata that accompanied the received message.
    /// </param>
    /// <param name="token">
    /// A cancellation token with which the handling of the message can be canceled.
    /// </param>
    /// <typeparam name="TMessage">
    /// The type of message that the message handler accepts.
    /// </typeparam>
    /// <returns>
    /// Returns a task, with which client code can wait for a message to be handled.
    /// </returns>
    public delegate Task MessageHandler<in TMessage>(IMessage<TMessage> message, MessageReceivedInfo info,
        CancellationToken token);
}
