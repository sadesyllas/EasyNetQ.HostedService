using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EasyNetQ.Consumer;
using EasyNetQ.HostedService.Abstractions;
using EasyNetQ.HostedService.Tracing;

namespace EasyNetQ.HostedService.Internals
{
    internal sealed class HandlerRegistrar : IHandlerRegistration
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

                using (var activity = _activitySource.StartActivity(TraceActivityName.Consume, ActivityKind.Consumer))
                {
                    if (activity != null)
                    {
                        activity
                            .AddTag(TraceActivityTagName.Exchange, messageReceivedInfo.Exchange)
                            .AddTag(TraceActivityTagName.RoutingKey, messageReceivedInfo.RoutingKey)
                            .AddTag(TraceActivityTagName.CorrelationId, message.Properties.CorrelationId)
                            .AddTag(TraceActivityTagName.DeliveryTag, messageReceivedInfo.DeliveryTag)
                            .AddTag(TraceActivityTagName.Redelivered, messageReceivedInfo.Redelivered)
                            .AddTag(TraceActivityTagName.Headers, message.Properties.Headers);
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
                                        new KeyValuePair<string, object>(TraceActivityTagName.Exception, exception)
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
