using System;
using Newtonsoft.Json.Linq;

namespace EasyNetQ.HostedService.Internals
{
    /// <summary>
    /// An override for the default EasyNetQ implementation of <see cref="IMessageSerializationStrategy"/>.
    ///
    /// It omits checking for the RabbitMQ message's <c>type</c> property, thus handling all messages as having a type
    /// of <see cref="string"/>.
    /// </summary>
    internal sealed class UntypedMessageSerializationStrategy : IMessageSerializationStrategy
    {
        private readonly bool _useCorrelationIds;
        private readonly ISerializer _serializer;
        private readonly ICorrelationIdGenerationStrategy _correlationIdGenerator;

        public UntypedMessageSerializationStrategy(
            bool useCorrelationIds,
            ISerializer serializer,
            ICorrelationIdGenerationStrategy correlationIdGenerator)
        {
            _useCorrelationIds = useCorrelationIds;
            _serializer = serializer;
            _correlationIdGenerator = correlationIdGenerator;
        }

        public SerializedMessage SerializeMessage(IMessage message)
        {
            var bytes = _serializer.MessageToBytes(message.MessageType, message.GetBody());
            var properties = message.Properties;

            if (_useCorrelationIds && string.IsNullOrWhiteSpace(properties.CorrelationId))
            {
                properties.CorrelationId = _correlationIdGenerator.GetCorrelationId();
            }

            return new SerializedMessage(properties, bytes);
        }

        public IMessage DeserializeMessage(MessageProperties properties, in ReadOnlyMemory<byte> body)
        {
            string message;
            switch (_serializer.BytesToMessage(typeof(object), body))
            {
                case string messageTmp:
                    message = messageTmp;
                    break;
                case var messageTmp:
                    message = ((JObject) messageTmp).ToString();
                    break;
            }

            return MessageFactory.CreateInstance(typeof(string), message, properties);
        }
    }
}
