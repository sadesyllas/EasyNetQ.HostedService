using System;
using System.Runtime.Serialization;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Internals
{
    public class UnhandledHeaderTypeExceptionWithRequeue : Exception, INackWithRequeueException
    {
        /// <inheritdoc />
        public UnhandledHeaderTypeExceptionWithRequeue() { }

        /// <inheritdoc />
        public UnhandledHeaderTypeExceptionWithRequeue(string message) : base(message) { }

        /// <inheritdoc />
        public UnhandledHeaderTypeExceptionWithRequeue(string format, params object[] args) : base(string.Format(format, args)) { }

        /// <inheritdoc />
        public UnhandledHeaderTypeExceptionWithRequeue(string message, Exception inner) : base(message, inner) { }

        /// <inheritdoc />
        protected UnhandledHeaderTypeExceptionWithRequeue(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}