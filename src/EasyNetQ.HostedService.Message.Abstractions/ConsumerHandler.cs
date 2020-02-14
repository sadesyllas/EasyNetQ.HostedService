using System;
using System.Threading;
using System.Threading.Tasks;

namespace EasyNetQ.HostedService.Message.Abstractions
{
    /// <summary>
    /// A helper class for manipulating consumer handlers written by the library's client.
    /// </summary>
    public static class ConsumerHandler
    {
        /// <summary>
        /// Wraps a consumer handler which accepts an <see cref="IMessage{TMessage}"/> within a handler that accepts an
        /// <see cref="IMessage"/>.
        ///
        /// This is done in order to use a common key/value data structure to hold the handler per consumer message
        /// type.
        /// </summary>
        /// <param name="handler"/>
        /// <typeparam name="TMessage"/>
        /// <returns>
        /// The wrapped handler to register as a consumer handler for a specific message type.
        /// </returns>
        public static Func<IMessage, MessageReceivedInfo, CancellationToken, Task> Wrap<TMessage>(
            Func<IMessage<TMessage>, MessageReceivedInfo, CancellationToken, Task> handler) =>
            (m, i, t) => handler((IMessage<TMessage>) m, i, t);
    }
}
