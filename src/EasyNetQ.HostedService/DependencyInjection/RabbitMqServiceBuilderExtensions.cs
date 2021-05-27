using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EasyNetQ.HostedService.DependencyInjection
{
    /// <summary>
    /// Extension methods for <see cref="RabbitMqServiceBuilder{T}"/>.
    /// </summary>
    public static class RabbitMqServiceBuilderExtensions
    {
        /// <summary>
        /// Registers a subclass of <see cref="RabbitMqService{T}"/> to be used with dependency injection.
        ///
        /// It registers the <see cref="RabbitMqService{T}"/> both as <paramref name="type"/> and optionally, as
        /// <paramref name="extraTypes"/>.
        /// </summary>
        /// <typeparam name="T">
        /// The consumer or producer subclass of <see cref="RabbitMqService{T}"/>.
        /// </typeparam>
        /// <param name="serviceFactory">
        /// The factory function that builds the <see cref="RabbitMqService{T}"/>.
        /// </param>
        /// <param name="serviceCollection">
        /// The <see cref="IServiceCollection"/> with which to register the <see cref="RabbitMqService{T}"/>.
        /// </param>
        /// <param name="type">
        /// The type with witch to register the <see cref="RabbitMqService{T}"/> in the
        /// <see cref="IServiceCollection"/>.
        /// </param>
        /// <param name="extraTypes">
        /// Optional, extra types with witch to register the <see cref="RabbitMqService{T}"/> in the
        /// <see cref="IServiceCollection"/>.
        /// </param>
        public static void Add<T>(this Func<IServiceProvider, T> serviceFactory, IServiceCollection serviceCollection,
            Type type, params Type[] extraTypes) where T : RabbitMqService<T>
        {
            serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService>(serviceFactory));

            var types = new[] {type}.Concat(extraTypes).ToArray();

            foreach (var t in types)
            {
                if (!t.IsAssignableFrom(typeof(T)))
                {
                    throw new Exception(
                        $"{nameof(t)}, of type {t.FullName}, must be assignable from " +
                        $"{nameof(T)}, of type {typeof(T).FullName}, to be used with dependency injection.");
                }
            }

            foreach (var t in types)
            {
                serviceCollection.AddSingleton(t, serviceFactory);
            }
        }
    }
}
