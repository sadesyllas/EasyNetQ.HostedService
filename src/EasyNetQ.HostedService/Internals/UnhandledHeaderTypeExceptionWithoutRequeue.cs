using System;
using System.Runtime.Serialization;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Internals
{
    public class UnhandledHeaderTypeExceptionWithoutRequeue : Exception, INackWithoutRequeueException
    {
        /// <inheritdoc />
        public UnhandledHeaderTypeExceptionWithoutRequeue() { }

        /// <inheritdoc />
        public UnhandledHeaderTypeExceptionWithoutRequeue(string message) : base(message) { }

        /// <inheritdoc />
        public UnhandledHeaderTypeExceptionWithoutRequeue(string format, params object[] args) : base(string.Format(format, args)) { }

        /// <inheritdoc />
        public UnhandledHeaderTypeExceptionWithoutRequeue(string message, Exception inner) : base(message, inner) { }

        /// <inheritdoc />
        protected UnhandledHeaderTypeExceptionWithoutRequeue(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}