using System;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Consumer;
using EasyNetQ.HostedService.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyNetQ.HostedService.Internals
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal sealed class ConsumerErrorStrategy : IConsumerErrorStrategy
    {
        private readonly ILogger<ConsumerErrorStrategy> _logger;
        private bool _disposed;
        private bool _disposing;

        public ConsumerErrorStrategy(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetService<ILogger<ConsumerErrorStrategy>>();
        }

        public Task<AckStrategy> HandleConsumerErrorAsync(
            ConsumerExecutionContext context, 
            Exception exception,
            CancellationToken cancellationToken)
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
                        return Task.FromResult(AckStrategies.NackWithRequeue);
                    case UnhandledMessageTypeException _:
                        _logger?.LogError($"Unhandled message type: {context.Properties.Type}");

                        return Task.FromResult(AckStrategies.NackWithoutRequeue);
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
                                return Task.FromResult(AckStrategies.Ack);
                            case INackWithRequeueException _:
                                return Task.FromResult(AckStrategies.NackWithRequeue);
                            default:
                                return Task.FromResult(AckStrategies.NackWithoutRequeue);
                        }
                }
            }

            message = $"The {nameof(ConsumerErrorStrategy)} has already been disposed, while attempting to handle a " +
                      "consumer error, and the received message ({info}) will be requeued.";

            _logger?.LogError(message, context.ReceivedInfo);

            return Task.FromResult(AckStrategies.NackWithRequeue);
        }

        public Task<AckStrategy> HandleConsumerCancelledAsync(
            ConsumerExecutionContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(AckStrategies.NackWithRequeue);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposing = true;
            _disposed = true;
        }
    }
}
