using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.HostedService.DependencyInjection;
using EasyNetQ.HostedService.Models;
using EasyNetQ.Topology;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EasyNetQ.HostedService
{
    /// <summary>
    /// The subclasses of <c>RabbitMqProducer&lt;T&gt;</c> are hosted services that can be registered through
    /// dependency injection.
    ///
    /// For a convenient way to inject such a producer hosted service, see <see cref="RabbitMqServiceBuilder"/>.
    ///
    /// Using <see cref="RabbitMqServiceBuilder"/>, allows one to inject such a producer as
    /// <c>RabbitMqProducer&lt;T&gt;</c>.
    /// </summary>
    /// <typeparam name="T">
    /// The subclass of <c>RabbitMqProducer&lt;T&gt;</c> that will be registered as a producer through
    /// dependency injection.
    /// </typeparam>
    /// <remarks>
    /// Any required services can be injected through constructor parameters as with any other hosted service.
    ///
    /// The default <see cref="RabbitMqProducer{T}"/> implementation uses a <see cref="ConcurrentQueue{T}"/> of
    /// <see cref="RabbitMqProducer{T}.Message"/> as the message queue and a separate <see cref="Task"/> to dequeue
    /// messages and send them to the RabbitMQ server.
    /// </remarks>
    /// /// <example>
    /// <code><![CDATA[
    /// // An example of a producer type.
    ///
    /// using Microsoft.Extensions.Hosting;
    ///
    /// namespace EasyNetQ.HostedService.TestApp
    /// {
    ///     public class MyInjectableRabbitMqProducer : RabbitMqProducer<MyInjectableRabbitMqProducer>
    ///     {
    ///         // optional constructor with additional injected dependencies
    ///         public MyInjectableRabbitMqProducer(IHostEnvironment env)
    ///         {
    ///             // do something with env
    ///         }
    ///     }
    /// }
    /// ]]></code>
    /// </example>
    public abstract class RabbitMqProducer<T> : RabbitMqService<T>
    {
        private readonly ConcurrentQueue<Message> _messages = new ConcurrentQueue<Message>();

        private readonly SemaphoreSlim _messageSemaphore = new SemaphoreSlim(0);

        private CancellationToken _cancellationToken;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="cancellationToken"/>
        protected internal override void InitializeProducer(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;

            LoadMessages();

            Logger?.LogDebug($"Starting producer loop with configuration {RabbitMqConfig.Id}.");

            StartProducerLoop(cancellationToken);
        }

        /// <summary>
        /// Enqueues a message of type <c>TMessage</c> for publishing after turning it into a <see cref="Message"/>.
        /// </summary>
        /// <param name="exchange"/>
        /// <param name="routingKey"/>
        /// <param name="payload"/>
        /// <param name="mandatory"/>
        /// <typeparam name="TMessage"/>
        /// <returns>
        /// It returns a <see cref="TaskCompletionSource{PublishResult}"/> which can be awaited until the message is
        /// actually sent to the RabbitMQ server.
        /// </returns>
        public virtual Task<PublishResult> PublishAsync<TMessage>(
            string exchange,
            string routingKey,
            TMessage payload,
            bool mandatory = false)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(PublishResult.NotPublished);
            }

            var payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
            var taskCompletionSource = new TaskCompletionSource<PublishResult>();
            var message = new Message
            {
                Exchange = exchange,
                RoutingKey = routingKey,
                Mandatory = mandatory,
                Payload = payloadBytes,
                Type = typeof(TMessage),
                TaskCompletionSource = taskCompletionSource,
            };

            _messages.Enqueue(message);

            _messageSemaphore.Release();

            return taskCompletionSource.Task;
        }

        /// <summary>
        /// If overriden it can persist enqueued but not yet sent messages.
        ///
        /// It is meant as a way to write messages to disk in case the service needs to stop.
        /// </summary>
        protected virtual void PersistMessages()
        {
            Logger?.LogTrace(
                $"When implemented, function {nameof(PersistMessages)} can store messages " +
                "to persistent storage.");
        }

        /// <summary>
        /// If overriden it can load and enqueue messages to be sent.
        ///
        /// It is meant as a way to load messages from disk when the service starts.
        /// </summary>
        protected virtual void LoadMessages()
        {
            Logger?.LogTrace(
                $"When implemented, function {nameof(LoadMessages)} can load messages " +
                "from persistent storage.");
        }

        private void StartProducerLoop(CancellationToken cancellationToken) =>
            Task.Factory.StartNew(state =>
            {
                while (true)
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _messages.ToList().ForEach(enqueuedMessage =>
                                enqueuedMessage.TaskCompletionSource.SetResult(PublishResult.NotPublished));

                            PersistMessages();

                            return;
                        }

                        _messageSemaphore.Wait(cancellationToken);

                        if (_messages.TryPeek(out var message))
                        {
                            var exchange = new Exchange(message.Exchange);

                            var properties = new MessageProperties
                            {
                                Type = $"{message.Type}, {message.Type.Assembly.GetName().Name}"
                            };

#if LOG_DEBUG_RABBITMQ_PRODUCER_PUBLISHED_MESSAGES
                            Logger?.LogDebug(
                                $"Publishing message to exchange {message.Exchange} with " +
                                $"routing key {message.RoutingKey} and payload {Encoding.UTF8.GetString(message.Payload)}.");
#endif

                            Bus.Publish(
                                exchange,
                                message.RoutingKey,
                                message.Mandatory,
                                properties,
                                message.Payload,
                                cancellationToken);

                            message.TaskCompletionSource.SetResult(PublishResult.Published);

                            _messages.TryDequeue(out _);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Logger?.LogDebug($"Stopping producer loop with configuration {RabbitMqConfig.Id}.");

                        return;
                    }
                    catch (TimeoutException)
                    {
                        Logger?.LogError(
                            $"Timeout occured while trying to publish with configuration {RabbitMqConfig.Id}.");

                        Task
                            .Delay(RabbitMqConfig.PublisherLoopErrorBackOffMilliseconds, cancellationToken)
                            .Wait(cancellationToken);

                        _messageSemaphore.Release();
                    }
                    catch (Exception exception)
                    {
                        Logger?.LogCritical(
                            $"Critical error in producer loop: {exception.Message}\n" +
                            $"{exception.StackTrace}");

                        Task
                            .Delay(RabbitMqConfig.PublisherLoopErrorBackOffMilliseconds, cancellationToken)
                            .Wait(cancellationToken);

                        _messageSemaphore.Release();
                    }
                }
            }, null, TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness);

        /// <summary>
        /// The message type that is actually enqueued in the <see cref="ConcurrentQueue{T}"/>.
        /// </summary>
        private sealed class Message
        {
            // ReSharper disable RedundantDefaultMemberInitializer

            public string Exchange { get; set; } = null!;
            public string RoutingKey { get; set; } = null!;
            public bool Mandatory { get; set; }
            public byte[] Payload { get; set; } = null!;
            public Type Type { get; set; } = null!;
            public TaskCompletionSource<PublishResult> TaskCompletionSource { get; set; } = null!;

            // ReSharper restore RedundantDefaultMemberInitializer
        }

        #region Consumer Implementation

        /// <summary>
        /// Expected to be overriden by consumers.
        ///
        /// The default implementation for producers returns <c>null</c>.
        /// </summary>
        protected sealed override IDictionary<Type, Func<IMessage, MessageReceivedInfo, CancellationToken, Task>>
            MessageHandlerMap => null!;

        /// <summary>
        /// Expected to be overriden by consumers.
        ///
        /// The default implementation for producers throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="cancellationToken"/>
        /// <exception cref="NotSupportedException"/>
        protected internal sealed override void InitializeConsumer(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        #endregion Consumer Implementation
    }
}
