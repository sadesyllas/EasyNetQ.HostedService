using System;
using System.Runtime.Serialization;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Internals
{
    public class UnhandledHeaderTypeException : Exception, INackWithRequeueException
    {
        /// <inheritdoc />
        public UnhandledHeaderTypeException() { }

        /// <inheritdoc />
        public UnhandledHeaderTypeException(string message) : base(message) { }

        /// <inheritdoc />
        public UnhandledHeaderTypeException(string format, params object[] args) : base(string.Format(format, args)) { }

        /// <inheritdoc />
        public UnhandledHeaderTypeException(string message, Exception inner) : base(message, inner) { }

        /// <inheritdoc />
        protected UnhandledHeaderTypeException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        
    }
}