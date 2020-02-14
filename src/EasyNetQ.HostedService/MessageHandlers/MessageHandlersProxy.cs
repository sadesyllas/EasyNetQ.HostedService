using EasyNetQ.HostedService.Message.Abstractions;
using EasyNetQ.HostedService.MessageHandlers.Impl;

namespace EasyNetQ.HostedService.MessageHandlers
{
    /// <summary>
    /// A proxy class to provide a window into the different implementations of <see cref="IMessageHandler{TMessage}"/>
    /// implemented by <see cref="MessageHandlersImpl"/>.
    /// </summary>
    internal static class MessageHandlersProxy
    {
        private static readonly MessageHandlersImpl MessageHandlersImpl = new MessageHandlersImpl();

        public static IMessageHandler<object> Default => MessageHandlersImpl;
    }
}
