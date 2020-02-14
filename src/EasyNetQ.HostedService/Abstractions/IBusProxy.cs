namespace EasyNetQ.HostedService.Abstractions
{
    /// <summary>
    /// A wrapper around an <see cref="IAdvancedBus"/> to bind it to an <see cref="IBusProxy.Id"/>, thus giving it a
    /// name.
    /// </summary>
    public interface IBusProxy
    {
        /// <summary>
        /// The <c>Id</c> of the configuration which names an instance of <see cref="IAdvancedBus"/>.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// An initialized <see cref="IAdvancedBus"/> instance.
        /// </summary>
        IAdvancedBus Bus { get; }
    }
}
