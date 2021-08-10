using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Consumer;
using EasyNetQ.Events;
using EasyNetQ.HostedService.DependencyInjection;
using EasyNetQ.HostedService.Internals;
using EasyNetQ.HostedService.Models;
using EasyNetQ.HostedService.Tracing;
using EasyNetQ.Internals;
using Newtonsoft.Json;
using RabbitMQ.Client;

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
        private readonly List<IDisposable> _startConsumingEventSubscriptions = new List<IDisposable>();

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
        protected virtual void OnStartConsumingEvent(StartConsumingSucceededEvent @event) =>
            Logger?.LogDebug(
                $"Started consuming from {@event.Queue.Name} with args: " +
                $"{JsonConvert.SerializeObject(@event.Queue.Arguments)}.");

        /// <summary>
        /// Registers an event handler for the <see cref="StartConsumingFailedEvent"/> event.
        /// </summary>
        /// <param name="event"></param>
        /// <remarks>
        /// This method can be overriden by classes derived from <see cref="RabbitMqConsumer{T}"/>.
        /// </remarks>
        protected virtual void OnStartConsumingEvent(StartConsumingFailedEvent @event)
        {
            var model = ExtractModelFromInternalConsumer(@event.Consumer, Logger);

            // If `model` is `null`, then there is no open channel with RabbitMQ.
            if (model != null)
            {
                Logger?.LogCritical(
                    $"Failed to consume from {@event.Queue.Name} ({@event.Queue.Arguments}) with " +
                    $"code {model.CloseReason.ReplyCode} and reason {model.CloseReason.ReplyText}.");
            }
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="cancellationToken"/>
        protected override void InitializeConsumer(CancellationToken cancellationToken)
        {
            SubscribeToStartConsumingEvent<StartConsumingSucceededEvent>(OnStartConsumingEvent);

            SubscribeToStartConsumingEvent<StartConsumingFailedEvent>(OnStartConsumingEvent);

            AddDisposable(StartConsuming(cancellationToken));
        }

        private void SubscribeToStartConsumingEvent<TEvent>(Action<TEvent> eventHandler)
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

            Debug.Assert(consumerConfig.Queue != null, $"{nameof(ConsumerConfig.Queue)} must not be null.");

            return Bus.Consume(consumerConfig.Queue, handlers =>
            {
                try
                {
                    RegisterMessageHandlers(new HandlerRegistrar(handlers, IncomingMessageInterceptor, ActivitySource));
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

        // TODO: THIS USE OF REFLECTION IS FRAGILE AND MUST BE RECONSIDERED
        private static IModel ExtractModelFromInternalConsumer(IConsumer consumer, TraceLogWriter logger)
        {
            IModel model = null;
            var consumerType = consumer.GetType();

            var internalConsumersField =
                consumerType.GetField("internalConsumers", BindingFlags.Instance | BindingFlags.NonPublic);

            if (internalConsumersField != null)
            {
                var internalConsumers = (ConcurrentSet<IInternalConsumer>) internalConsumersField.GetValue(consumer);
                // ReSharper disable once AssignNullToNotNullAttribute
                // ReSharper disable once ConstantConditionalAccessQualifier
                model = ((InternalConsumer) internalConsumers.FirstOrDefault())?.Model;
            }

            var internalConsumerField =
                consumerType.GetField("internalConsumer", BindingFlags.Instance | BindingFlags.NonPublic);

            if (internalConsumerField != null)
            {
                var internalConsumer = (IInternalConsumer) internalConsumerField.GetValue(consumer);
                // ReSharper disable once ConstantConditionalAccessQualifier
                model = ((InternalConsumer) internalConsumer)?.Model;
            }

            if (model == null)
            {
                logger?.LogCritical(
                    $"Could not extract a non null {nameof(IModel)} " +
                    $"from the provided {nameof(IConsumer)}.");
            }

            return model;
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
