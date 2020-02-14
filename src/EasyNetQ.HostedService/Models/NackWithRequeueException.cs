using System;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Models
{
    /// <summary>
    /// <inheritdoc cref="INackWithRequeueException"/>
    /// </summary>
    public class NackWithRequeueException : Exception, INackWithRequeueException
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="message"/>
        public NackWithRequeueException(string? message) : base(message)
        {
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="message"/>
        /// <param name="innerException"/>
        public NackWithRequeueException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
