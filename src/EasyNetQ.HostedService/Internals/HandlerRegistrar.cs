using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EasyNetQ.Consumer;
using EasyNetQ.HostedService.Abstractions;
using EasyNetQ.HostedService.Tracing;

namespace EasyNetQ.HostedService.Internals
{
    internal sealed class HandlerRegistrar<TConsumer> : IHandlerRegistration
    {
        private readonly IHandlerRegistration _handlers;
        private readonly IIncomingMessageInterceptor _incomingMessageInterceptor;
        private readonly ActivitySource _activitySource;

        internal HandlerRegistrar(IHandlerRegistration handlers, IIncomingMessageInterceptor incomingMessageInterceptor,
            ActivitySource activitySource)
        {
            _handlers = handlers;
            _incomingMessageInterceptor = incomingMessageInterceptor;
            _activitySource = activitySource;
        }

        public IHandlerRegistration Add<T>(IMessageHandler<T> handler)
        {
            return _handlers.Add(async (IMessage<T> message, MessageReceivedInfo messageReceivedInfo,
                CancellationToken cancellationToken) =>
            {
                if (_incomingMessageInterceptor != null)
                {
                    await _incomingMessageInterceptor.InterceptMessage(message, messageReceivedInfo, cancellationToken);
                }

                var maybeTraceId = message?.Properties.Headers?["X-TRACE-ID"];
                var traceId = maybeTraceId is string ? (string)maybeTraceId : null;

                using (var activity =
                    _activitySource.StartActivity($"{typeof(TConsumer).FullName} receive", ActivityKind.Consumer,
                        // ReSharper disable once AssignNullToNotNullAttribute
                        traceId))
                {
                    if (activity != null)
                    {
                        activity
                            .AddTag("messaging.system", "rabbitmq")
                            .AddTag("messaging.operation", "receive")
                            .AddTag("messaging.rabbitmq.routing_key", messageReceivedInfo.RoutingKey)
                            .AddTag("x-messaging.rabbitmq.correlation_id", message?.Properties.CorrelationId)
                            .AddTag("x-messaging.rabbitmq.delivery_tag", messageReceivedInfo.DeliveryTag)
                            .AddTag("x-messaging.rabbitmq.redelivered", messageReceivedInfo.Redelivered)
                            .AddTag("x-messaging.rabbitmq.headers", message?.Properties.Headers);

                        if ((message?.Properties.Headers?.ContainsKey("X-MESSAGE-ID")).GetValueOrDefault())
                        {
                            activity.AddTag("messaging.message_id", message?.Properties.Headers?["X-MESSAGE-ID"]);
                        }
                    }

                    try
                    {
                        return await handler(message, messageReceivedInfo, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        if (activity != null)
                        {
                            activity.AddEvent(new ActivityEvent(TraceEventName.Exception,
                                tags: new ActivityTagsCollection(
                                    new[]
                                    {
                                        new KeyValuePair<string, object>("exception", exception)
                                    })));
                        }

                        throw;
                    }
                }
            });
        }

        public bool ThrowOnNoMatchingHandler
        {
            get => _handlers.ThrowOnNoMatchingHandler;
            set => _handlers.ThrowOnNoMatchingHandler = value;
        }
    }
}
