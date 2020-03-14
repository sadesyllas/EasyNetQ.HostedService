using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Consumer;
using EasyNetQ.Events;
using EasyNetQ.HostedService.Abstractions;
using EasyNetQ.HostedService.DependencyInjection;
using EasyNetQ.HostedService.Internals;
using EasyNetQ.HostedService.Internals.Extensions;
using EasyNetQ.HostedService.MessageHandlers;
using EasyNetQ.Internals;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using static EasyNetQ.HostedService.Internals.MessageHandlerHelper;

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
    /// using System;
    /// using System.Collections.Generic;
    /// using System.Threading.Tasks;
    /// using EasyNetQ.HostedService.Message.Abstractions;
    /// using Microsoft.Extensions.Hosting;
    /// using Microsoft.Extensions.Logging;
    /// using static EasyNetQ.HostedService.Message.Abstractions.MessageHandlerHelper;
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
    ///         public void MyInjectableRabbitMqConsumer(IHostEnvironment env)
    ///         {
    ///             // do something with env
    ///         }
    ///
    ///         protected override IDictionary<Type, MessageHandler> MessageHandlerMap =>
    ///             new Dictionary<Type, MessageHandler>
    ///             {
    ///                 {
    ///                     typeof(string),
    ///                     MessageHandlerHelper.Wrap<string>((message, info, token) =>
    ///                     {
    ///                         Logger.LogDebug($"Received message: {message.Body}");
    ///
    ///                         return Task.CompletedTask;
    ///                     })
    ///                 }
    ///             };
    ///
    ///         protected override void Initialize()
    ///         {
    ///             // use initialized members like `Bus` and `RabbitMqConfig`
    ///         }
    /// }
    /// ]]></code>
    /// </example>
    public abstract partial class RabbitMqConsumer<T> : RabbitMqService<T>
    {
        private IDisposable? _startConsumingDisposable;
        private IDisposable? _startConsumingSucceededEventSubscription;
        private IDisposable? _startConsumingFailedEventSubscription;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            DisposeStartConsumingSucceededEventSubscription();

            DisposeStartConsumingFailedEventSubscription();

            return base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="cancellationToken"/>
        protected override void InitializeConsumer(CancellationToken cancellationToken)
        {
            SubscribeToStartConsumingSucceededEvent(@event =>
                Logger?.LogDebug(
                    $"Started consuming from {@event.Queue.Name} " +
                    $"({JsonConvert.SerializeObject(@event.Queue.Arguments)})."));

            SubscribeToStartConsumingFailedEvent(@event =>
            {
                var model = ExtractModelFromInternalConsumer(@event.Consumer, Logger);

                Logger?.LogCritical(
                    $"Failed to consume from {@event.Queue.Name} ({@event.Queue.Arguments}) with " +
                    $"code {model?.CloseReason.ReplyCode} and reason {model?.CloseReason.ReplyText}.");
            });

            if (Bus.IsConnected)
            {
                AddDisposable(StartConsuming(cancellationToken));
            }

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

        /// <summary>
        /// Registers an event handler for the <see cref="StartConsumingSucceededEvent"/> event.
        /// </summary>
        /// <param name="eventHandler"></param>
        /// <remarks>
        /// This function first calls <see cref="DisposeStartConsumingSucceededEventSubscription"/> to dispose of the
        /// existing <see cref="_startConsumingSucceededEventSubscription"/> instance, if any.
        ///
        /// If this consumer is using a shared <see cref="IAdvancedBus"/> instance, then the event handler will receive
        /// events from other <see cref="StartConsumingSucceededEvent"/> subscriptions as well.
        /// </remarks>
        protected void SubscribeToStartConsumingSucceededEvent(Action<StartConsumingSucceededEvent> eventHandler)
        {
            DisposeStartConsumingSucceededEventSubscription();

            var eventBus = Bus.Container.Resolve<IEventBus>();

            _startConsumingSucceededEventSubscription = eventBus.Subscribe(eventHandler);
        }

        /// <summary>
        /// Registers an event handler for the <see cref="StartConsumingFailedEvent"/> event.
        /// </summary>
        /// <param name="eventHandler"></param>
        /// <remarks>
        /// This function first calls <see cref="DisposeStartConsumingFailedEventSubscription"/> to dispose of the
        /// existing <see cref="_startConsumingFailedEventSubscription"/> instance, if any.
        ///
        /// If this consumer is using a shared <see cref="IAdvancedBus"/> instance, then the event handler will receive
        /// events from other <see cref="StartConsumingFailedEvent"/> subscriptions as well.
        /// </remarks>
        protected void SubscribeToStartConsumingFailedEvent(Action<StartConsumingFailedEvent> eventHandler)
        {
            DisposeStartConsumingFailedEventSubscription();

            var eventBus = Bus.Container.Resolve<IEventBus>();

            _startConsumingFailedEventSubscription = eventBus.Subscribe(eventHandler);
        }

        /// <summary>
        /// Disposes of the <see cref="_startConsumingSucceededEventSubscription"/>.
        /// </summary>
        protected void DisposeStartConsumingSucceededEventSubscription() =>
            _startConsumingSucceededEventSubscription?.Dispose();

        /// <summary>
        /// Disposes of the <see cref="_startConsumingFailedEventSubscription"/>.
        /// </summary>
        protected void DisposeStartConsumingFailedEventSubscription() =>
            _startConsumingFailedEventSubscription?.Dispose();

        private IDisposable StartConsuming(CancellationToken cancellationToken)
        {
            var queue = RabbitMqConfig.Queue;

            Debug.Assert(queue != null, $"{nameof(IRabbitMqConfig.Queue)} must not be null.");

            return Bus.Consume(RabbitMqConfig.DeclaredQueue ?? queue.AsIQueue, handlers =>
            {
                // generic method `IHandlerRegistration.Add<TMessage>`
                var addMethodInfo = handlers.GetType().GetMethod("Add");

                Debug.Assert(addMethodInfo != null, "Method Add not found in bus handlers collection object.");

                // generic method `MessageHandlerHelper.Wrap<TMessage>`
                var wrapMethodInfo = typeof(MessageHandlerHelper).GetMethod("Wrap");

                Debug.Assert(wrapMethodInfo != null, "Method Add not found in bus handlers collection object.");

                // generic method `MessageHandlerExtensions.ToGenericMessageHandler<TMessage>`
                var toGenericMessageHandlerMethodInfo =
                    typeof(MessageHandlerExtensions).GetMethod("ToGenericMessageHandler");

                Debug.Assert(toGenericMessageHandlerMethodInfo != null,
                    $"Method ToGenericMessageHandler not found in {nameof(MessageHandlerExtensions)}");

                // generic method `MessageHandlerExtensions.ToFunc<TMessage>`
                var toFuncMethodInfo = typeof(MessageHandlerExtensions).GetMethod("ToFunc");

                Debug.Assert(toFuncMethodInfo != null,
                    $"Method ToFunc not found in {nameof(MessageHandlerExtensions)}");

                Debug.Assert(MessageHandlerMap != null,
                    $"{nameof(MessageHandlerMap)} returned null in {nameof(InitializeConsumer)}");

                // each handler is of type `MessageHandler`
                foreach (var (type, handler) in MessageHandlerMap)
                {
                    // set `type` as the type argument for a parameterized instance of the generic method
                    // MessageHandlerExtensions.ToGenericMessageHandler<TMessage>
                    var genericToGenericMessageHandlerMethod =
                        toGenericMessageHandlerMethodInfo.MakeGenericMethod(type);

                    // invoke the parameterized MessageHandlerExtensions.ToGenericMessageHandler<TMessage> to turn the
                    // `MessageHandler` into a `MessageHandler<TMessage>`
                    var typedHandler = genericToGenericMessageHandlerMethod.Invoke(null, new[] {(object) handler});

                    // set `type` as the type argument for a parameterized instance of the generic method
                    // `MessageHandlerHelper.Wrap<TMessage>`
                    var genericWrapMethod = wrapMethodInfo.MakeGenericMethod(type);

                    // wrap the `MessageHandler<TMessage>` handler in a try/catch statement
                    var wrappedHandler = genericWrapMethod.Invoke(null,
                        new[] {typedHandler, Bus, RabbitMqConfig, cancellationToken});

                    // set `type` as the type argument for a parameterized instance of the generic method
                    // `MessageHandlerExtensions.ToFunc<TMessage>`
                    var genericToFuncMethod = toFuncMethodInfo.MakeGenericMethod(type);

                    // turn a `MessageHandler<TMessage>` into a
                    // `Func<IMessage<TMessage>, MessageReceivedInfo, CancellationToken, Task>`
                    var wrappedHandlerFunc = genericToFuncMethod.Invoke(null, new[] {wrappedHandler});
                    
                    // set `type` as the type argument for a parameterized instance of the generic method
                    // `IHandlerRegistration.Add`
                    var genericAddMethod = addMethodInfo.MakeGenericMethod(type);

                    // add a message handler of type
                    // `Func<IMessage<TMessage>, MessageReceivedInfo, CancellationToken, Task>` to the
                    // `IHandlerRegistration` instance
                    genericAddMethod.Invoke(handlers, new[] {wrappedHandlerFunc});
                }

                if (!MessageHandlerMap.ContainsKey(typeof(object)))
                {
                    handlers.Add(
                        Wrap<object>(MessageHandlersProxy.Default.HandleMessage, Bus, RabbitMqConfig, cancellationToken)
                            .ToFunc());
                }
            }, config =>
            {
                if (queue.Priority.HasValue)
                {
                    config.WithPriority(queue.Priority.Value);
                }

                config.WithExclusive(queue.ConsumeExclusive);

                if (queue.PrefetchCount.HasValue)
                {
                    config.WithPrefetchCount(queue.PrefetchCount.Value);
                }
            });
        }

        // TODO: THIS USE OF REFLECTION IS FRAGILE AND MUST BE RECONSIDERED
        private static IModel? ExtractModelFromInternalConsumer(IConsumer consumer, ILogger? logger)
        {
            IModel? model = null;
            var consumerType = consumer.GetType();

            var internalConsumersField =
                consumerType.GetField("internalConsumers", BindingFlags.Instance | BindingFlags.NonPublic);

            if (internalConsumersField != null)
            {
                var internalConsumers = (ConcurrentSet<IInternalConsumer>) internalConsumersField.GetValue(consumer);
                model = ((InternalConsumer) internalConsumers.FirstOrDefault())?.Model;
            }

            var internalConsumerField =
                consumerType.GetField("internalConsumer", BindingFlags.Instance | BindingFlags.NonPublic);

            if (internalConsumerField != null)
            {
                var internalConsumer = (IInternalConsumer) internalConsumerField.GetValue(consumer);
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
