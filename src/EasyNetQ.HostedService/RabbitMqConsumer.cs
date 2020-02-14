using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using EasyNetQ.Consumer;
using EasyNetQ.Events;
using EasyNetQ.HostedService.DependencyInjection;
using EasyNetQ.HostedService.Internals;
using EasyNetQ.HostedService.MessageHandlers;
using EasyNetQ.Internals;
using EasyNetQ.Topology;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace EasyNetQ.HostedService
{
    /// <summary>
    /// The subclasses of <c>RabbitMqConsumer&lt;T&gt;</c> are hosted services that can be registered through
    /// dependency injection.
    ///
    /// For a convenient way to inject such a consumer hosted service, see <see cref="RabbitMqServiceBuilder"/>.
    ///
    /// Using <see cref="RabbitMqServiceBuilder"/>, allows one to inject such a consumer as
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
    /// using System.Text.Json;
    /// using System.Threading;
    /// using System.Threading.Tasks;
    /// using AMLRecordsIndexer.RabbitMQ.Messages;
    /// using EasyNetQ;
    /// using EasyNetQ.HostedService;
    /// using EasyNetQ.HostedService.Message.Abstractions;
    /// using Microsoft.Extensions.Hosting;
    /// using JsonSerializer = System.Text.Json.JsonSerializer;
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
    ///         protected override IDictionary<Type, Func<IMessage, MessageReceivedInfo, CancellationToken, Task>>
    ///             MessageHandlerMap =>
    ///             new Dictionary<Type, Func<IMessage, MessageReceivedInfo, CancellationToken, Task>>
    ///             {
    ///                 {
    ///                     typeof(string),
    ///                     ConsumerHandler.Wrap<string>((message, info, arg3) =>
    ///                     {
    ///                         var msg = System.Text.Json.JsonSerializer.Deserialize<EchoMessage>(message.Body, new JsonSerializerOptions()
    ///                         {
    ///                             PropertyNameCaseInsensitive = true,
    ///                         });
    ///
    ///                         Console.WriteLine($"Received simple message: {message.Body}");
    ///                         Console.WriteLine($"Received simple deserialized message: {msg.Text}");
    ///
    ///                         return Task.CompletedTask;
    ///                     })
    ///                 }
    ///             };
    ///
    ///     }
    /// }
    /// ]]></code>
    /// </example>
    public abstract class RabbitMqConsumer<T> : RabbitMqService<T>
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="cancellationToken"/>
        protected internal override void InitializeConsumer(CancellationToken cancellationToken)
        {
            var eventBus = Bus.Container.Resolve<IEventBus>();

            AddDisposable(
                eventBus.Subscribe<StartConsumingSucceededEvent>(@event =>
                    Logger?.LogDebug(
                        $"Started consuming from {@event.Queue.Name} " +
                        $"({JsonConvert.SerializeObject(@event.Queue.Arguments)}).")));

            AddDisposable(
                eventBus.Subscribe<StartConsumingFailedEvent>(@event =>
                {
                    var model = ExtractModelFromInternalConsumer(@event.Consumer, Logger);

                    Logger?.LogCritical(
                        $"Failed to consume from {@event.Queue.Name} ({@event.Queue.Arguments}) with " +
                        $"code {model?.CloseReason.ReplyCode} and reason {model?.CloseReason.ReplyText}.");
                }));

            if (Bus.IsConnected)
            {
                AddDisposable(
                    StartConsuming(RabbitMqConfig.DeclaredQueue ?? RabbitMqConfig.Queue.AsIQueue, cancellationToken));
            }

            Bus.Connected += (sender, args) =>
            {
                AddDisposable(
                    StartConsuming(RabbitMqConfig.DeclaredQueue ?? RabbitMqConfig.Queue.AsIQueue, cancellationToken));
            };
        }

        private IDisposable StartConsuming(IQueue queue, CancellationToken cancellationToken) =>
            Bus.Consume(queue, handlers =>
            {
                var addMethodInfo = handlers.GetType().GetMethod("Add");

                Debug.Assert(addMethodInfo != null, "Method Add not found in bus handlers collection object.");

                var wrapMethodInfo = typeof(ConsumerHandler).GetMethod("Wrap");

                Debug.Assert(wrapMethodInfo != null, "Method Add not found in bus handlers collection object.");

                Debug.Assert(MessageHandlerMap != null,
                    $"{nameof(MessageHandlerMap)} returned null in {nameof(InitializeConsumer)}");

                foreach (var (type, handler) in MessageHandlerMap)
                {
                    var genericWrapMethod = wrapMethodInfo.MakeGenericMethod(type);

                    var wrappedHandler = genericWrapMethod.Invoke(
                        null, new object[] {handler, Bus, RabbitMqConfig, cancellationToken});

                    addMethodInfo.MakeGenericMethod(type).Invoke(handlers, new[] {wrappedHandler});
                }

                if (!MessageHandlerMap.ContainsKey(typeof(object)))
                {
                    handlers.Add(ConsumerHandler.Wrap<object>(
                        MessageHandlersProxy.Default.HandleMessage, Bus, RabbitMqConfig, cancellationToken));
                }
            }, config =>
            {
                if (RabbitMqConfig.Queue.Priority.HasValue)
                {
                    config.WithPriority(RabbitMqConfig.Queue.Priority.Value);
                }

                config.WithExclusive(RabbitMqConfig.Queue.ConsumeExclusive);

                if (RabbitMqConfig.Queue.PrefetchCount.HasValue)
                {
                    config.WithPrefetchCount(RabbitMqConfig.Queue.PrefetchCount.Value);
                }
            });

        // TODO: THIS USE OF REFLECTION IS FRAGILE AND MUST BE RECONSIDERED
        private static IModel? ExtractModelFromInternalConsumer(IConsumer consumer, ILogger? logger)
        {
            IModel? model = null;
            var consumerType = consumer.GetType();

            var internalConsumersField =
                consumerType.GetField("internalConsumers", BindingFlags.Instance | BindingFlags.NonPublic);

            if (internalConsumersField != null)
            {
                var internalConsumers = (ConcurrentSet<IInternalConsumer>)internalConsumersField.GetValue(consumer);
                model = ((InternalConsumer)internalConsumers.FirstOrDefault()).Model;
            }

            var internalConsumerField =
                consumerType.GetField("internalConsumer", BindingFlags.Instance | BindingFlags.NonPublic);

            if (internalConsumerField != null)
            {
                var internalConsumer = (IInternalConsumer)internalConsumerField.GetValue(consumer);
                model = ((InternalConsumer)internalConsumer)?.Model;
            }

            if (model == null)
            {
                logger?.LogCritical(
                    $"Could not extract a non null {nameof(IModel)} " +
                    $"from the provided {nameof(IConsumer)}.");
            }

            return model;
        }

        #region Producer Implementation

        /// <summary>
        /// Expected to be overriden by producers.
        ///
        /// The default implementation for consumers throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="cancellationToken"/>
        /// <exception cref="NotSupportedException"/>
        protected internal sealed override void InitializeProducer(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        #endregion Producer Implementation
    }
}
