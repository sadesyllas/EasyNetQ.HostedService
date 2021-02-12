namespace EasyNetQ.HostedService.Models
{
    /// <summary>
    /// The result of calling <see cref="RabbitMqProducer{T}.PublishAsync{TMessage}"/>.
    /// </summary>
    public enum PublishResult : byte
    {
        /// <summary>
        /// The message was published successfully.
        /// </summary>
        Published = 1,

        /// <summary>
        /// The message was not published successfully because an error occured.
        /// </summary>
        NotPublished = 2,
    }
}
