namespace EasyNetQ.HostedService.Abstractions
{
    /// <summary>
    /// When thrown from a consumer's handler, an ACK will be sent for the message to the RabbitMQ server.
    /// </summary>
    public interface IAckException
    {
    }
}
