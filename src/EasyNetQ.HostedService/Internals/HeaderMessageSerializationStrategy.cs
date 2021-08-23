using System;
using System.Linq;

namespace EasyNetQ.HostedService.Internals
{
    public class HeaderMessageSerializationStrategy : IMessageSerializationStrategy
    {
        private readonly bool _useCorrelationIds;
        private readonly ISerializer _serializer;
        private readonly ICorrelationIdGenerationStrategy _correlationIdGenerator;
        private readonly HeaderTypeSerializationConfiguration _headerTypeSerializationConfiguration;
        
        public HeaderMessageSerializationStrategy(
            bool useCorrelationIds,
            HeaderTypeSerializationConfiguration headerTypeSerializationConfiguration,
            ISerializer serializer,
            ICorrelationIdGenerationStrategy correlationIdGenerator)
        {
            _useCorrelationIds = useCorrelationIds;
            _serializer = serializer;
            _correlationIdGenerator = correlationIdGenerator;
            _headerTypeSerializationConfiguration = headerTypeSerializationConfiguration;
        }
        
        public SerializedMessage SerializeMessage(IMessage message)
        {
            var bytes = _serializer.MessageToBytes(message.MessageType, message.GetBody());
            var properties = message.Properties;

            var typeHeader = _headerTypeSerializationConfiguration
                .TypeMappings
                .SingleOrDefault(v => message.MessageType == v.Value)
                .Key;

            if (string.IsNullOrEmpty(typeHeader))
            {
                throw new EasyNetQException(
                    $"Did not find a unique mapping for the specified type {message.MessageType}");
            }
            
            properties.Headers.Add(_headerTypeSerializationConfiguration.TypeHeader, typeHeader);
            
            if (_useCorrelationIds && string.IsNullOrEmpty(properties.CorrelationId))
            {
                properties.CorrelationId = _correlationIdGenerator.GetCorrelationId();
            }

            return new SerializedMessage(properties, bytes);
        }

        public IMessage DeserializeMessage(MessageProperties properties, byte[] body)
        {
            var typeHeaderValueExists = properties.Headers.TryGetValue(
                _headerTypeSerializationConfiguration.TypeHeader,
                out var typeFromHeader);

            if (!typeHeaderValueExists)
            {
                throw new UnhandledHeaderTypeException("Type header not present on message");
            }

            var typeExistsInMapping = _headerTypeSerializationConfiguration.TypeMappings.TryGetValue(
                System.Text.Encoding.Default.GetString((byte[])typeFromHeader),
                out var messageType);

            if (!typeExistsInMapping)
            {
                throw new UnhandledHeaderTypeException("Message type from header not found in mapping");
            }
            
            var message = _serializer.BytesToMessage(messageType, body);

            return MessageFactory.CreateInstance(messageType, message, properties);
        }
    }
}