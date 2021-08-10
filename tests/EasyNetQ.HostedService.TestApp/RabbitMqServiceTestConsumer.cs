using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Consumer;
using EasyNetQ.Events;
using EasyNetQ.HostedService.Models;
using EasyNetQ.Topology;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace EasyNetQ.HostedService.TestApp
{
    public class RabbitMqServiceTestConsumer : RabbitMqConsumer<RabbitMqServiceTestConsumer>,
        IRabbitMqServiceTestConsumer
    {
        private readonly IQueue _queue = new Queue(Program.TestQueueName);
        private readonly TaskCompletionSource<string> _taskCompletionSourceUntyped = new TaskCompletionSource<string>();

        private readonly TaskCompletionSource<EchoMessage> _taskCompletionSourceTyped =
            new TaskCompletionSource<EchoMessage>();

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        public RabbitMqServiceTestConsumer(IHostEnvironment env)
        {
            Debug.Assert(env != null, $"{nameof(env)} must not be null.");
        }

        public Task<string> UntypedResponse => _taskCompletionSourceUntyped.Task;

        public Task<EchoMessage> TypedResponse => _taskCompletionSourceTyped.Task;

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            var baseTask = base.StopAsync(cancellationToken);

            return baseTask;
        }

        protected override void Initialize()
        {
            Bus.QueueDeclare(_queue.Name, _queue.IsDurable, _queue.IsExclusive, _queue.IsAutoDelete,
                CancellationToken.None);
        }

        protected override void RegisterMessageHandlers(IHandlerRegistration handlers)
        {
            handlers.Add((IMessageHandler<string>) HandleMessage);
            handlers.Add((IMessageHandler<EchoMessage>) HandleMessage);
        }

        protected override ConsumerConfig GetConsumerConfig(CancellationToken cancellationToken)
        {
            return new ConsumerConfig
            {
                Queue = _queue
            };
        }

        protected override void OnStartConsumingEvent(StartConsumingFailedEvent @event)
        {
            throw new Exception($"Should not be able to consume with a conflicted queue declaration.");
        }

        private Task<AckStrategy> HandleMessage(IMessage<string> message, MessageReceivedInfo info,
            CancellationToken token)
        {
            Logger.LogDebug($"Received untyped message: {message.Body}");

            _taskCompletionSourceUntyped.SetResult(new string(message.Body.Reverse().ToArray()));

            return Task.FromResult(AckStrategies.Ack);
        }

        private Task<AckStrategy> HandleMessage(IMessage<EchoMessage> message, MessageReceivedInfo info,
            CancellationToken token)
        {
            var typedMessage = message.Body;

            Logger.LogDebug($"Received typed message: {JsonConvert.SerializeObject(typedMessage)}");

            typedMessage.Text = new string(typedMessage.Text.Reverse().ToArray());

            _taskCompletionSourceTyped.SetResult(typedMessage);

            return Task.FromResult(AckStrategies.Ack);
        }
    }
}
