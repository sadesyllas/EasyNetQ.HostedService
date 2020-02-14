using System;
using EasyNetQ.Consumer;
using EasyNetQ.HostedService.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyNetQ.HostedService.Internals
{
    internal sealed class ConsumerErrorStrategy : DefaultConsumerErrorStrategy
    {
        private readonly ILogger<ConsumerErrorStrategy>? _logger;
        private bool _disposed;
        private bool _disposing;

        public ConsumerErrorStrategy(
            IPersistentConnection connection,
            ISerializer serializer,
            IConventions conventions,
            ITypeNameSerializer typeNameSerializer,
            IErrorMessageSerializer errorMessageSerializer,
            IServiceProvider serviceProvider) : base(
            connection, serializer, conventions, typeNameSerializer, errorMessageSerializer)
        {
            _logger = serviceProvider.GetService<ILogger<ConsumerErrorStrategy>>();
        }

        public override AckStrategy HandleConsumerError(ConsumerExecutionContext context, Exception exception)
        {
            if (!_disposed && !_disposing)
            {
                var consumerException = exception switch
                {
                    ConsumerException ce => ce,
                    var other => new ConsumerException(other)
                };

                switch (consumerException.InnerException)
                {
                    case OperationCanceledException _:
                        return AckStrategies.NackWithRequeue;
                    case UnhandledMessageTypeException _:
                        _logger?.LogError($"Unhandled message type: {context.Properties.Type}");

                        return AckStrategies.NackWithoutRequeue;
                    default:
                        _logger?.LogError(
                            $"Consumer exception: {consumerException.InnerException?.Message}\n" +
                            $"{consumerException.InnerException?.StackTrace}");

                        return consumerException.InnerException switch
                        {
                            IAckException _ => AckStrategies.Ack,
                            INackWithRequeueException _ => AckStrategies.NackWithRequeue,
                            INackWithoutRequeueException _ => AckStrategies.NackWithoutRequeue,
                            _ => AckStrategies.NackWithoutRequeue
                        };
                }
            }

            var message =
                $"The {nameof(ConsumerErrorStrategy)} has already been disposed, while attempting to handle a " +
                "consumer error, and the received message ({info}) will be requeued.";

            _logger?.LogError(message, (object)context.Info);

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
