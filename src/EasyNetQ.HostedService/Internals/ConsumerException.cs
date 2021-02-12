using System;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Internals
{
    internal sealed class ConsumerException : Exception
    {
        public ConsumerException(Exception innerException) : base(nameof(ConsumerException), innerException)
        {
        }

        public IAdvancedBus? RmqBus { get; set; }

        public IRabbitMqConfig? RabbitMqConfig { get; set; }
    }
}
