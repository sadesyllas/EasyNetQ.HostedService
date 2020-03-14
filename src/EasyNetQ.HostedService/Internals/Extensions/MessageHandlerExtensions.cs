using System;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.HostedService.Message.Abstractions;

namespace EasyNetQ.HostedService.Internals.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="MessageHandler{TMessage}"/>.
    /// </summary>
    public static class MessageHandlerExtensions
    {
        /// <summary>
        /// Converts a <see cref="MessageHandler"/> into a
        /// <see cref="Func{T1, T2, T3, TResult}"/> where <c>T1</c> is of type <see cref="IMessage{TMessage}"/>.
        /// </summary>
        /// <param name="messageHandler">
        /// The message handle to convert.
        /// </param>
        /// <typeparam name="TMessage">
        /// The type of messages that the <paramref name="messageHandler"/> handles.
        /// </typeparam>
        public static MessageHandler<TMessage> ToGenericMessageHandler<TMessage>(this MessageHandler messageHandler) =>
            new MessageHandler<TMessage>(messageHandler);

        /// <summary>
        /// Converts a <see cref="MessageHandler{TMessage}"/> into a
        /// <see cref="Func{T1, T2, T3, TResult}"/> where <c>T1</c> is of type <see cref="IMessage{TMessage}"/>.
        /// </summary>
        /// <param name="messageHandler">
        /// The message handle to convert.
        /// </param>
        /// <typeparam name="TMessage">
        /// The type of messages that the <paramref name="messageHandler"/> handles.
        /// </typeparam>
        public static Func<IMessage<TMessage>, MessageReceivedInfo, CancellationToken, Task> ToFunc<TMessage>(
            this MessageHandler<TMessage> messageHandler) =>
            new Func<IMessage<TMessage>, MessageReceivedInfo, CancellationToken, Task>(messageHandler);
    }
}
