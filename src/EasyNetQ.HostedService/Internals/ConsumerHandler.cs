using System;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Internals
{
    /// <summary>
    /// A helper class for manipulating consumer handlers written by the library's client.
    /// </summary>
    internal static class ConsumerHandler
    {
        /// <summary>
        /// Wraps a consumer's handler in order to wrap the call to the initial handler in a try/catch clause.
        ///
        /// In case of an exception, a <see cref="ConsumerException"/> is always thrown, which provides the current
        /// <see cref="IAdvancedBus"/> and <see cref="IRabbitMqConfig"/> to the <see cref="ConsumerErrorStrategy"/>.
        /// </summary>
        /// <param name="handler"/>
        /// <param name="rmqBus"/>
        /// <param name="rabbitMqConfig"/>
        /// <param name="cancellationToken"/>
        /// <typeparam name="TMessage"/>
        /// <returns/>
        public static Func<IMessage<TMessage>, MessageReceivedInfo, CancellationToken, Task> Wrap<TMessage>(
            Func<IMessage<TMessage>, MessageReceivedInfo, CancellationToken, Task> handler,
            IAdvancedBus rmqBus,
            IRabbitMqConfig rabbitMqConfig,
            CancellationToken cancellationToken) =>
            (m, i, t) =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return handler(m, i, t);
                }
                catch (Exception exception)
                {
                    throw new ConsumerException(exception)
                    {
                        RmqBus = rmqBus,
                        RabbitMqConfig = rabbitMqConfig
                    };
                }
            };
    }
}
