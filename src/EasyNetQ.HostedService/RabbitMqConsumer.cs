using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Events;
using EasyNetQ.HostedService.DependencyInjection;
using EasyNetQ.HostedService.Internals;
using EasyNetQ.HostedService.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EasyNetQ.HostedService
{
    /// <summary>
    /// The subclasses of <c>RabbitMqConsumer&lt;T&gt;</c> are hosted services that can be registered through
    /// dependency injection.
    ///
    /// For a convenient way to inject such a consumer hosted service, see <see cref="RabbitMqServiceBuilder{T}"/>.
    ///
    /// Using <see cref="RabbitMqServiceBuilder{T}"/>, allows one to inject such a consumer as
    /// <c>RabbitMqConsumer&lt;T&gt;</c>.
    /// </summary>
    /// <typeparam name="T">
    /// The subclass of <c>RabbitMqConsumer&lt;T&gt;</c> that will be registered as a consumer through
    /// dependency injection.
    /// </typeparam>
    /// <remarks>
    /// Any required services can be injected through constructor parameters as with any other hosted service.
    /// </remarks>
    /// <example>
    /// <code><![CDATA[
    /// // An example of a consumer type.
    ///
    ///  using System.Threading;
    ///  using System.Threading.Tasks;
    ///  using EasyNetQ.Consumer;
    ///  using EasyNetQ.Events;
    ///  using EasyNetQ.HostedService.Models;
    ///  using EasyNetQ.Topology;
    ///  using Microsoft.Extensions.Hosting;
    ///  using Microsoft.Extensions.Logging;
    ///
    /// namespace EasyNetQ.HostedService.TestApp
    /// {
    ///     public struct EchoMessage
    ///     {
    ///         public string Text { get; set; }
    ///     }
    ///
    ///     public class MyInjectableRabbitMqConsumer : RabbitMqConsumer<MyInjectableRabbitMqConsumer>
    ///     {
    ///         // optional constructor with additional injected dependencies
    ///         public MyInjectableRabbitMqConsumer(IHostEnvironment env)
    ///         {
    ///             // do something with env
    ///         }
    ///
    ///         protected override void Initialize()
    ///         {
    ///             // use initialized members like Bus and RabbitMqConfig (eg, Bus.QueueDeclare(...))
    ///         }
    ///
    ///         protected override void RegisterMessageHandlers(IHandlerRegistration handlers)
    ///         {
    ///             handlers.Add((IMessageHandler<string>) HandleMessage);
    ///         }
    ///
    ///         protected override ConsumerConfig GetConsumerConfig(CancellationToken cancellationToken)
    ///         {
    ///             return new ConsumerConfig
    ///             {
    ///                 Queue = new Queue("MyQueueName")
    ///             };
    ///         }
    ///
    ///         protected override void OnStartConsumingEvent(StartConsumingSucceededEvent @event)
    ///         {
    ///             // the consumer has successfully started to consume
    ///         }
    ///
    ///         protected override void OnStartConsumingEvent(StartConsumingFailedEvent @event)
    ///         {
    ///             // the consumer has failed to start consuming
    ///         }
    ///
    ///         private Task<AckStrategy> HandleMessage(IMessage<string> message, MessageReceivedInfo info,
    ///             CancellationToken token)
    ///         {
    ///             Logger.LogDebug($"Received untyped message: {message.Body}");
    ///
    ///             return Task.FromResult(AckStrategies.Ack);
    ///         }
    ///     }
    /// }
    /// ]]></code>
    /// </example>
    public abstract partial class RabbitMqConsumer<T> : RabbitMqService<T>
    {
        private IDisposable _startConsumingDisposable;
        private List<IDisposable> _startConsumingEventSubscriptions = new List<IDisposable>();

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            DisposeStartConsumingEventSubscriptions();

            return base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// This method must be provided by classes derived from <see cref="RabbitMqConsumer{T}"/>, in order to provide
        /// the necessary <see cref="ConsumerConfig"/> to start consuming.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract ConsumerConfig GetConsumerConfig(CancellationToken cancellationToken);

        /// <summary>
        /// Registers an event handler for the <see cref="StartConsumingSucceededEvent"/> event.
        /// </summary>
        /// <param name="event"></param>
        /// <remarks>
        /// This method can be overriden by classes derived from <see cref="RabbitMqConsumer{T}"/>.
        /// </remarks>
        protected virtual void OnStartConsumingEvent(in StartConsumingSucceededEvent @event) =>
            Logger?.LogDebug("Started consuming from {queueName} with args: {queueArgs}.",
                @event.Queue.Name,
                JsonConvert.SerializeObject(@event.Queue.Arguments));

        /// <summary>
        /// Registers an event handler for the <see cref="StartConsumingFailedEvent"/> event.
        /// </summary>
        /// <param name="event"></param>
        /// <remarks>
        /// This method can be overriden by classes derived from <see cref="RabbitMqConsumer{T}"/>.
        /// </remarks>
        protected virtual void OnStartConsumingEvent(in StartConsumingFailedEvent @event)
        {
            Logger?.LogCritical("Failed to consume from {queueName} ({queueArguments})",
                @event.Queue.Name,
                JsonConvert.SerializeObject(@event.Queue.Arguments));
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="cancellationToken"/>
        protected override void InitializeConsumer(CancellationToken cancellationToken)
        {
            SubscribeToStartConsumingEvent<StartConsumingSucceededEvent>(OnStartConsumingEvent);

            SubscribeToStartConsumingEvent<StartConsumingFailedEvent>(OnStartConsumingEvent);

            _startConsumingDisposable = StartConsuming(cancellationToken);

            AddDisposable(_startConsumingDisposable);

            Bus.Connected += (sender, args) =>
            {
                try
                {
                    _startConsumingDisposable?.Dispose();
                }
                catch (Exception exception)
                {
                    Logger?.LogError(
                        $"Could not dispose of {nameof(_startConsumingDisposable)} in {nameof(RabbitMqConsumer<T>)}: " +
                        $"{exception.Message}\n{exception.StackTrace}");
                }

                _startConsumingDisposable = StartConsuming(cancellationToken);

                AddDisposable(_startConsumingDisposable);
            };
        }

        private void SubscribeToStartConsumingEvent<TEvent>(TEventHandler<TEvent> eventHandler) where TEvent : struct
        {
            var eventBus = Bus.Container.Resolve<IEventBus>();

            _startConsumingEventSubscriptions.Add(eventBus.Subscribe(eventHandler));
        }

        private void DisposeStartConsumingEventSubscriptions()
        {
            _startConsumingEventSubscriptions.ForEach(subscription => subscription?.Dispose());

            _startConsumingEventSubscriptions.Clear();
        }

        // ReSharper disable once UnusedParameter.Local
        private IDisposable StartConsuming(CancellationToken cancellationToken)
        {
            var consumerConfig = GetConsumerConfig(cancellationToken);

            Debug.Assert(consumerConfig != null, $"{nameof(ConsumerConfig)} must not be null.");
            
            return Bus.Consume(consumerConfig.Queue, handlers =>
            {
                try
                {
                    RegisterMessageHandlers(new HandlerRegistrar(handlers, IncomingMessageInterceptor));
                }
                catch (Exception exception)
                {
                    Logger?.LogError(
                        $"Could not register message handlers: {exception.Message}\n{exception.StackTrace}");

                    throw;
                }
            }, config =>
            {
                if (consumerConfig.Priority.HasValue)
                {
                    config.WithPriority(consumerConfig.Priority.Value);
                }

                config.WithExclusive(consumerConfig.IsExclusive);

                if (consumerConfig.PrefetchCount.HasValue)
                {
                    config.WithPrefetchCount(consumerConfig.PrefetchCount.Value);
                }
            });
        }
    }

    #region Producer Implementation

    public abstract partial class RabbitMqConsumer<T>
    {
        /// <summary>
        /// Expected to be overriden by producers.
        ///
        /// The default implementation for consumers throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="cancellationToken"/>
        /// <exception cref="NotSupportedException"/>
        protected sealed override void InitializeProducer(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    #endregion Producer Implementation

}
