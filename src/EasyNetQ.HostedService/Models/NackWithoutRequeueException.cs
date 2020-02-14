using System;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Models
{
    /// <summary>
    /// <inheritdoc cref="INackWithoutRequeueException"/>
    /// </summary>
    public class NackWithoutRequeueException : Exception, INackWithoutRequeueException
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="message"/>
        public NackWithoutRequeueException(string? message) : base(message)
        {
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="message"/>
        /// <param name="innerException"/>
        public NackWithoutRequeueException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
