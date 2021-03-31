using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Consumer;
using EasyNetQ.HostedService.DependencyInjection;
using EasyNetQ.HostedService.Models;
using EasyNetQ.Topology;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client.Exceptions;

namespace EasyNetQ.HostedService
{
    /// <summary>
    /// The subclasses of <c>RabbitMqProducer&lt;T&gt;</c> are hosted services that can be registered through
    /// dependency injection.
    ///
    /// For a convenient way to inject such a producer hosted service, see <see cref="RabbitMqServiceBuilder{T}"/>.
    ///
    /// Using <see cref="RabbitMqServiceBuilder{T}"/>, allows one to inject such a producer as
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
    ///
    ///         protected override void Initialize()
    ///         {
    ///             // use initialized members like Bus and RabbitMqConfig
    ///         }
    ///     }
    /// }
    /// ]]></code>
    /// </example>
    public abstract partial class RabbitMqProducer<T> : RabbitMqService<T>
    {
        private readonly ConcurrentQueue<Message> _messages = new ConcurrentQueue<Message>();
        private readonly SemaphoreSlim _messageSemaphore = new SemaphoreSlim(0);
        private CancellationToken _cancellationToken;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="cancellationToken"/>
        protected sealed override void InitializeProducer(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;

            Logger?.LogDebug($"Starting producer loop with configuration {RabbitMqConfig.Id}.");

            OnInitialization(cancellationToken);

            StartProducerLoop(cancellationToken);
        }

        /// <summary>
        /// Enqueues a message of type <c>TMessage</c> for publishing after turning it into a <see cref="Message"/>.
        /// </summary>
        /// <param name="exchange"/>
        /// <param name="routingKey"/>
        /// <param name="payload"/>
        /// <param name="mandatory"/>
        /// <param name="headers"></param>
        /// <typeparam name="TMessage"/>
        /// <exception cref="ArgumentException"></exception>
        /// <returns>
        /// It returns a <see cref="TaskCompletionSource{PublishResult}"/> which can be awaited until the message is
        /// actually sent to the RabbitMQ server.
        /// </returns>
        public virtual Task<PublishResult> PublishAsync<TMessage>(
            string exchange,
            string routingKey,
            TMessage payload,
            bool mandatory = false,
            IDictionary<string, object> headers = null)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(PublishResult.NotPublished);
            }

            if (exchange == null)
            {
                throw new ArgumentException("The exchange must not be null.");
            }

            if (routingKey == null)
            {
                throw new ArgumentException("The routing key must not be null.");
            }

            if (payload == null)
            {
                throw new ArgumentException("The payload must not be null.");
            }

            var payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

            var taskCompletionSource =
                new TaskCompletionSource<PublishResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            var message = new Message
            {
                Exchange = exchange,
                RoutingKey = routingKey,
                Mandatory = mandatory,
                Payload = payloadBytes,
                Type = typeof(TMessage),
                Headers = headers,
                TaskCompletionSource = taskCompletionSource,
            };

            EnqueueMessage(message);

            _messageSemaphore.Release();

            return taskCompletionSource.Task;
        }

        /// <summary>
        /// Called by <see cref="InitializeProducer"/> before calling <see cref="StartProducerLoop"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// The cancellation token with which <see cref="InitializeProducer"/> has been called.
        /// </param>
        private protected virtual void OnInitialization(CancellationToken cancellationToken)
        {
        }

        /// <summary>
        /// Enqueues a message to be sent by the producer.
        ///
        /// This method is called by <see cref="PublishAsync{TMessage}"/>.
        /// </summary>
        /// <remarks>
        /// One can override this method to provide an alternative mechanism for enqueuing the messages.
        ///
        /// If this method is overriden, <see cref="DequeueMessage"/> should also be overriden.
        /// </remarks>
        private protected virtual void EnqueueMessage(Message message) => _messages.Enqueue(message);

        /// <summary>
        /// Dequeues a message to be sent by the producer.
        ///
        /// This method is called by by the producer's message loop thread.
        /// </summary>
        /// <param name="message">
        /// The message to enqueue.
        /// </param>
        /// <returns>
        /// Returns <c>True</c> if a message was successfully dequeued, <c>False</c> otherwise.
        /// </returns>
        /// <remarks>
        /// One can override this method to provide an alternative mechanism for dequeuing the messages.
        ///
        /// If this method is overriden, <see cref="EnqueueMessage"/> should also be overriden.
        /// </remarks>
        private protected virtual bool DequeueMessage(out Message message) => _messages.TryPeek(out message);

        /// <summary>
        /// This method is called by the producer's message loop thread, which started by
        /// <see cref="StartProducerLoop"/>, after cancellation has been requested on the
        /// <see cref="CancellationToken"/> with which <see cref="InitializeProducer"/> has been called.
        /// </summary>
        private protected virtual void OnCancellation()
        {
        }

        private void StartProducerLoop(CancellationToken cancellationToken) =>
            new Thread(() =>
            {
                while (true)
                {
                    Message message = null;

                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _messages.ToList().ForEach(enqueuedMessage =>
                                enqueuedMessage.TaskCompletionSource.SetResult(PublishResult.NotPublished));

                            OnCancellation();

                            return;
                        }

                        // SemaphoreSlim.Wait disposes of the CancellationTokenRegistration
                        _messageSemaphore.Wait(cancellationToken);

                        if (DequeueMessage(out message))
                        {
                            var exchange = new Exchange(message.Exchange);

                            var properties = new MessageProperties
                            {
                                Type = $"{message.Type}, {message.Type.Assembly.GetName().Name}",
                                Headers = message.Headers
                            };

#if LOG_DEBUG_RABBITMQ_PRODUCER_PUBLISHED_MESSAGES
                            Logger?.LogDebug(
                                $"Publishing message to exchange {message.Exchange} " +
                                $"({GetMessageInformation(message)}) with routing key {message.RoutingKey} and payload " +
                                $"{Encoding.UTF8.GetString(message.Payload)}.");
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
                            $"Timeout occured while trying to publish with configuration " +
                            $"{RabbitMqConfig.Id} ({GetMessageInformation(message)})");

                        ProducerLoopWaitAndContinue(cancellationToken);
                    }
                    catch (AlreadyClosedException exception)
                    {
                        Logger?.LogError(
                            $"AMQP error in producer loop: {exception.Message}\n{exception.StackTrace}\n" +
                            $"({GetMessageInformation(message)})");

                        // 404 - NOT FOUND means that the message cannot be processed at all at this point and it's
                        // best to notify the library's client and discard it
                        if (exception.ShutdownReason?.ReplyCode == 404)
                        {
                            ProducerLoopDiscardMessageAndContinue(message);
                        }
                    }
                    catch (Exception exception)
                    {
                        Logger?.LogCritical(
                            $"Critical error in producer loop: {exception.Message}\n{exception.StackTrace}\n" +
                            $"({GetMessageInformation(message)})");

                        ProducerLoopWaitAndContinue(cancellationToken);
                    }
                }
            }).Start();

        private void ProducerLoopWaitAndContinue(CancellationToken cancellationToken)
        {
            Task.Delay(RabbitMqConfig.PublisherLoopErrorBackOffMilliseconds, cancellationToken).Wait(cancellationToken);

            _messageSemaphore.Release();
        }

        private void ProducerLoopDiscardMessageAndContinue(Message message)
        {
            message?.TaskCompletionSource.SetResult(PublishResult.NotPublished);

            _messages.TryDequeue(out _);
        }

        private string GetMessageInformation(Message message) =>
            $"publisher confirms: {RabbitMqConfig.PublisherConfirms}, " +
            $"{message?.ToString() ?? "- no message information available -"}";

        /// <summary>
        /// The message type that is actually enqueued in the <see cref="ConcurrentQueue{T}"/>.
        /// </summary>
        private protected sealed class Message
        {
            // ReSharper disable RedundantDefaultMemberInitializer

            public string Exchange { get; set; } = null;
            public string RoutingKey { get; set; } = null;
            public bool Mandatory { get; set; }
            public byte[] Payload { get; set; } = null;
            public IDictionary<string, object> Headers { get; set; } = null;
            public Type Type { get; set; } = null;
            public TaskCompletionSource<PublishResult> TaskCompletionSource { get; set; } = null;

            // ReSharper restore RedundantDefaultMemberInitializer

            public override string ToString() =>
                $"exchange: {Exchange}, routing key: {RoutingKey}, publisher confirms: mandatory: {Mandatory}";
        }
    }

    #region Consumer Implementation

    public abstract partial class RabbitMqProducer<T>
    {
        /// <summary>
        /// Expected to be overriden by consumers.
        ///
        /// The default implementation for producers throws a <see cref="NotImplementedException"/>.
        /// </summary>
        protected override void RegisterMessageHandlers(IHandlerRegistration handlers)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Expected to be overriden by consumers.
        ///
        /// The default implementation for producers throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="cancellationToken"/>
        /// <exception cref="NotSupportedException"/>
        protected sealed override void InitializeConsumer(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    #endregion Consumer Implementation
}
