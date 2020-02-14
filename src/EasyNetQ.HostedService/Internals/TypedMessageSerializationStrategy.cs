namespace EasyNetQ.HostedService.Internals
{
    /// <summary>
    /// An override for the default EasyNetQ implementation of <see cref="IMessageSerializationStrategy"/>.
    ///
    /// It uses a <see cref="ITypeNameSerializer"/> to deserialize the message's type from the RabbitMQ message's
    /// <c>type</c> property.
    /// </summary>
    internal class TypedMessageSerializationStrategy : IMessageSerializationStrategy
    {
        private readonly bool _useCorrelationIds;
        private readonly ITypeNameSerializer _typeNameSerializer;
        private readonly ISerializer _serializer;
        private readonly ICorrelationIdGenerationStrategy _correlationIdGenerator;

        public TypedMessageSerializationStrategy(
            bool useCorrelationIds,
            ITypeNameSerializer typeNameSerializer,
            ISerializer serializer,
            ICorrelationIdGenerationStrategy correlationIdGenerator)
        {
            _useCorrelationIds = useCorrelationIds;
            _typeNameSerializer = typeNameSerializer;
            _serializer = serializer;
            _correlationIdGenerator = correlationIdGenerator;
        }

        public SerializedMessage SerializeMessage(IMessage message)
        {
            var str = _typeNameSerializer.Serialize(message.MessageType);
            var bytes = _serializer.MessageToBytes(message.MessageType, message.GetBody());
            var properties = message.Properties;

            properties.Type = str;

            if (_useCorrelationIds && string.IsNullOrEmpty(properties.CorrelationId))
            {
                properties.CorrelationId = _correlationIdGenerator.GetCorrelationId();
            }

            return new SerializedMessage(properties, bytes);
        }

        public IMessage DeserializeMessage(MessageProperties properties, byte[] body)
        {
            var messageType = _typeNameSerializer.DeSerialize(properties.Type);
            var message = _serializer.BytesToMessage(messageType, body);

            return MessageFactory.CreateInstance(messageType, message, properties);
        }
    }
}
