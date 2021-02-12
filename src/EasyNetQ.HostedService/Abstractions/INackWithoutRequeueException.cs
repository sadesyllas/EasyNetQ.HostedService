namespace EasyNetQ.HostedService.Abstractions
{
    /// <summary>
    /// When thrown from a consumer's handler, a NACK will be sent for the message to the RabbitMQ server but the
    /// message will not be requeued.
    /// </summary>
    public interface INackWithoutRequeueException
    {
    }
}
