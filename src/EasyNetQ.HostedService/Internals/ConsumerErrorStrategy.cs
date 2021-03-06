using System;
using EasyNetQ.Consumer;
using EasyNetQ.HostedService.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyNetQ.HostedService.Internals
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal sealed class ConsumerErrorStrategy : DefaultConsumerErrorStrategy
    {
        private readonly ILogger<ConsumerErrorStrategy> _logger;
        private bool _disposed;
        private bool _disposing;

        public ConsumerErrorStrategy(
            IPersistentConnection connection,
            ISerializer serializer,
            IConventions conventions,
            ITypeNameSerializer typeNameSerializer,
            IErrorMessageSerializer errorMessageSerializer,
            ConnectionConfiguration connectionConfiguration,
            IServiceProvider serviceProvider) : base(
            connection, serializer, conventions, typeNameSerializer, errorMessageSerializer, connectionConfiguration)
        {
            _logger = serviceProvider.GetService<ILogger<ConsumerErrorStrategy>>();
        }

        public override AckStrategy HandleConsumerError(ConsumerExecutionContext context, Exception exception)
        {
            string message;

            if (!_disposed && !_disposing)
            {
                ConsumerException consumerException;
                switch (exception)
                {
                    case ConsumerException ce:
                        consumerException = ce;
                        break;
                    case var other:
                        consumerException = new ConsumerException(other);
                        break;
                }

                switch (consumerException.InnerException)
                {
                    case OperationCanceledException _:
                        return AckStrategies.NackWithRequeue;
                    case UnhandledMessageTypeException _:
                        _logger?.LogError($"Unhandled message type: {context.Properties.Type}");

                        return AckStrategies.NackWithoutRequeue;
                    default:
                        var innerException = consumerException.InnerException;
                        var innerInnerException = consumerException.InnerException?.InnerException;

                        if (!string.IsNullOrWhiteSpace(innerException?.Message) || innerInnerException != null)
                        {
                            message = $"Consumer exception: {innerException?.Message}\n{innerException?.StackTrace}";

                            _logger?.LogError(consumerException, message);
                        }

                        switch (consumerException.InnerException)
                        {
                            case IAckException _:
                                return AckStrategies.Ack;
                            case INackWithRequeueException _:
                                return AckStrategies.NackWithRequeue;
                            default:
                                return AckStrategies.NackWithoutRequeue;
                        }
                }
            }

            message = $"The {nameof(ConsumerErrorStrategy)} has already been disposed, while attempting to handle a " +
                      "consumer error, and the received message ({info}) will be requeued.";

            _logger?.LogError(message, context.ReceivedInfo);

            return AckStrategies.NackWithRequeue;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (_disposed)
            {
                return;
            }

            _disposing = true;
            _disposed = true;
        }
    }
}
