using System;
using EasyNetQ.HostedService.Abstractions;

namespace EasyNetQ.HostedService.Models
{
    /// <summary>
    /// <inheritdoc cref="IBusProxy"/>
    /// </summary>
    /// <remarks>
    /// This is the default <see cref="IBusProxy"/> implementation and it offers lazy instantiation of the
    /// <see cref="IAdvancedBus"/> property.
    /// </remarks>
    internal sealed class BusProxy : IBusProxy
    {
        private readonly Lazy<IAdvancedBus> _bus;

        public BusProxy(string id, Lazy<IAdvancedBus> bus)
        {
            Id = id;
            _bus = bus;
        }

        public string Id { get; }
        public IAdvancedBus Bus => _bus.Value;
    }
}
