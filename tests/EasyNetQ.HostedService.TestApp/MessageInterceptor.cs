using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.HostedService.Abstractions;
using Microsoft.Extensions.Logging;

namespace EasyNetQ.HostedService.TestApp
{
    public class MessageInterceptor : IIncomingMessageInterceptor<RabbitMqServiceTestConsumer>,
        IOutgoingMessageInterceptor<RabbitMqServiceTestProducer>
    {
        private readonly ILogger<MessageInterceptor> _logger;

        public MessageInterceptor(ILogger<MessageInterceptor> logger)
        {
            _logger = logger;
        }

        public Task InterceptMessage<TM>(IMessage<TM> message, MessageReceivedInfo messageReceivedInfo,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Intercepted incoming message of type {message.MessageType.FullName}.");

            return Task.CompletedTask;
        }

        public Task InterceptMessage(byte[] message, Type type, IDictionary<string, object> headers,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Intercepted outgoing message of type {type.FullName}.");

            return Task.CompletedTask;
        }
    }
}
