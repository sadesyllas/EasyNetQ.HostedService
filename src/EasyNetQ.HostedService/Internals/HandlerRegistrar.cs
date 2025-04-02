using System.Threading;
using EasyNetQ.Consumer;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Internals
{
    internal sealed class HandlerRegistrar : IHandlerRegistration
    {
        private readonly IHandlerRegistration _handlers;
        private readonly IIncomingMessageInterceptor _incomingMessageInterceptor;

        internal HandlerRegistrar(IHandlerRegistration handlers, IIncomingMessageInterceptor incomingMessageInterceptor)
        {
            _handlers = handlers;
            _incomingMessageInterceptor = incomingMessageInterceptor;
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

                return await handler(message, messageReceivedInfo, cancellationToken);
            });
        }

        public bool ThrowOnNoMatchingHandler
        {
            set => _handlers.ThrowOnNoMatchingHandler = value;
        }
    }
}
