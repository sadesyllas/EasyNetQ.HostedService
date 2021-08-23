using System;
using System.Collections.Generic;
using System.Linq;
using EasyNetQ.HostedService.Abstractions;
using EasyNetQ.HostedService.Internals;
using EasyNetQ.HostedService.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EasyNetQ.HostedService.DependencyInjection
{
    /// <summary>
    /// This class implements the builder pattern for subclasses of <see cref="RabbitMqService{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The consumer or producer subclass of <see cref="RabbitMqService{T}"/>.
    /// </typeparam>
    /// <example>
    /// <code>
    /// // in the configuration callback of <see cref="IHostBuilder.ConfigureServices"/>
    /// new RabbitMqServiceBuilder&lt;MyRabbitMqService&gt;()
    ///     .WithRabbitMqConfig(rabbitMqConfig)
    ///     .Build(services)
    ///     .Add(services, typeof(MyInjectedRabbitMqServiceType), typeof(MyInjectedRabbitMqServiceOptionalExtraType))
    /// </code>
    /// </example>
    public sealed class RabbitMqServiceBuilder<T> where T : RabbitMqService<T>
    {
        private MessageSerializationStrategy _configUseStronglyTypedMessages = MessageSerializationStrategy.UnTyped;
        private bool _configUseCorrelationIds;
        private HeaderTypeSerializationConfiguration _headerTypeSerializationConfiguration;
        private IRabbitMqConfig _configRabbitMqConfig;
        private readonly List<OnConnectedCallback> _configOnConnected = new List<OnConnectedCallback>();

        /// <summary>
        /// Enable <see cref="TypedMessageSerializationStrategy"/>.
        ///
        /// For consumers, it means that each incoming message is expected to have a valid <c>type</c> property,
        /// pointing to an available type in a <c>FULL_TYPE_NAME, ASSEMBLY_NAME</c> format, in order to deserialize the
        /// message into that type.
        ///
        /// For producers, it means that the message will be sent along with a valid <c>type</c> property in a
        /// <c>FULL_TYPE_NAME, ASSEMBLY_NAME</c> format.
        /// </summary>
        public RabbitMqServiceBuilder<T> WithStronglyTypedMessages
        {
            get
            {
                _configUseStronglyTypedMessages = MessageSerializationStrategy.Typed;

                return this;
            }
        }
        
        public RabbitMqServiceBuilder<T> WithHeaderTypedMessages(HeaderTypeSerializationConfiguration configuration)
        {
            _configUseStronglyTypedMessages = MessageSerializationStrategy.Header;
            _headerTypeSerializationConfiguration = configuration;
            return this;
        }

        /// <summary>
        /// When set, producers will send a correlation id along with each message.
        /// </summary>
        public RabbitMqServiceBuilder<T> WithCorrelationIds
        {
            get
            {
                _configUseCorrelationIds = true;

                return this;
            }
        }

        /// <summary>
        /// Sets the <see cref="IRabbitMqConfig"/> for the registered <see cref="RabbitMqService{T}"/>.
        /// </summary>
        public RabbitMqServiceBuilder<T> WithRabbitMqConfig(IRabbitMqConfig rabbitMqConfig)
        {
            // make sure that we are not using the same IRabbitMqConfig instance as the argument
            // ReSharper disable once ConstantConditionalAccessQualifier
            _configRabbitMqConfig = rabbitMqConfig?.Copy ?? new RabbitMqConfig();

            return this;
        }

        /// <summary>
        /// Adds a callback to run each time the <see cref="IAdvancedBus"/> is connected to the RabbitMQ server.
        /// </summary>
        public RabbitMqServiceBuilder<T> OnConnected(OnConnectedCallback callback)
        {
            _configOnConnected.Add(callback);

            return this;
        }

        /// <summary>
        /// Adds a callback to run each time the <see cref="IAdvancedBus"/> is connected to the RabbitMQ server.
        ///
        /// The callback is wrapped into a closure that makes sure that the callback is run only once.
        /// </summary>
        public RabbitMqServiceBuilder<T> OnConnectedOnce(OnConnectedCallback callback)
        {
            OnConnectedCallback CallbackOnceFactory()
            {
                var done = false;
                var @lock = new object();

                return (bus, rabbitMqConfig, cancellationToken, logger) =>
                {
                    if (done)
                    {
                        return;
                    }

                    lock (@lock)
                    {
                        if (done)
                        {
                            return;
                        }

                        done = true;

                        callback(bus, rabbitMqConfig, cancellationToken, logger);
                    }
                };
            }

            _configOnConnected.Add(CallbackOnceFactory());

            return this;
        }

        /// <summary>
        /// Builds a <see cref="ServiceDescriptor"/>, describing a singleton <see cref="IHostedService"/>, to be used
        /// with dependency injection.
        /// </summary>
        /// <remarks>
        /// It reuses an existing <see cref="IBusProxy"/> singleton, if one is found using the provided
        /// <see cref="IRabbitMqConfig.Id"/>.
        ///
        /// If not, a new <see cref="IBusProxy"/> singleton is registered in the service factory wrapped by the
        /// <see cref="ServiceDescriptor"/>.
        /// </remarks>
        public Func<IServiceProvider, T> Build(IServiceCollection serviceCollection)
        {
            var isConsumer = typeof(T).IsSubclassOf(typeof(RabbitMqConsumer<T>));

            if (!isConsumer && !typeof(T).IsSubclassOf(typeof(RabbitMqProducer<T>)))
            {
                throw new Exception(
                    $"{nameof(T)}, of type {typeof(T).FullName}, must be a subclass of " +
                    $"{nameof(RabbitMqConsumer<T>)} or {nameof(RabbitMqProducer<T>)}.");
            }

            if (_configRabbitMqConfig == null)
            {
                _configRabbitMqConfig = new RabbitMqConfig();
            }

            var busProxy = serviceCollection
                .Where(serviceDescriptor =>
                    serviceDescriptor.Lifetime == ServiceLifetime.Singleton &&
                    serviceDescriptor.ServiceType == typeof(IBusProxy))
                .Select(serviceDescriptor => (IBusProxy) serviceDescriptor.ImplementationInstance)
                .FirstOrDefault(registeredBusProxy => registeredBusProxy.Id == _configRabbitMqConfig.Id);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // ReSharper disable HeuristicUnreachableCode
            if (busProxy == null)
            {
                // build an IServiceProvider early so as to ensure that an ILogger<T> can be passed to
                // ConsumerErrorStrategy
                var serviceProvider =
                    new DefaultServiceProviderFactory(new ServiceProviderOptions
                        {
                            ValidateScopes = false
                        })
                        .CreateBuilder(serviceCollection)
                        .BuildServiceProvider();

                var bus = RabbitMqService<T>.CreateLazyBus(
                    _configRabbitMqConfig, _configUseStronglyTypedMessages, _configUseCorrelationIds, _headerTypeSerializationConfiguration, serviceProvider);

                busProxy = new BusProxy(_configRabbitMqConfig.Id, bus);

                serviceCollection.AddSingleton(busProxy);
            }
            // ReSharper restore HeuristicUnreachableCode

            Func<IServiceProvider, T> BuildServiceFactoryFactory()
            {
                T service = null;
                var @lock = new object();

                return serviceProvider =>
                {
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (service != null)
                    {
                        return service;
                    }

                    lock (@lock)
                    {
                        if (service != null)
                        {
                            return service;
                        }

                        // _configRabbitMqConfig must not be null here
                        service = RabbitMqService<T>.Create<T>(
                            isConsumer, busProxy, _configRabbitMqConfig, _configOnConnected, serviceProvider);

                        return service;
                    }
                };
            }

            return BuildServiceFactoryFactory();
        }
    }
}
