using System;

namespace EasyNetQ.HostedService.Internals
{
    /// <summary>
    /// Thrown when a consumer's <see cref="IMessage{TMessage}"/> default handler for <see cref="object"/> is run, since
    /// that means that no other consumer handler matched the message type.
    /// </summary>
    internal sealed class UnhandledMessageTypeException : Exception
    {
    }
}
