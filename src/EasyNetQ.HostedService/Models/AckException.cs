using System;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Models
{
    /// <summary>
    /// <inheritdoc cref="IAckException"/>
    /// </summary>
    public class AckException : Exception, IAckException
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="message"/>
        public AckException(string? message = "") : base(message)
        {
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <param name="message"/>
        /// <param name="innerException"/>
        public AckException(Exception? innerException, string? message = "") : base(message, innerException)
        {
        }
    }
}
