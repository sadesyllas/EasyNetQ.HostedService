using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EasyNetQ.HostedService.Abstractions;
using EasyNetQ.HostedService.Internals;
using EasyNetQ.HostedService.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyNetQ.HostedService.DependencyInjection
{
    /// <summary>
    /// This class implements the builder pattern for subclasses of <see cref="RabbitMqService{T}"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <example>
    /// <code>
    /// // in the configuration callback of <see cref="IHostBuilder.ConfigureServices"/>
    /// new RabbitMqServiceBuilder()
    ///     .WithRabbitMqConfig(rabbitMqConfig)
    ///     .Add&lt;MyRabbitMqService&gt;(services)
    /// </code>
    /// </example>
    public sealed class RabbitMqServiceBuilder<T> where T : RabbitMqService<T>
    {
        private bool _configUseStronglyTypedMessages;
        private bool _configUseCorrelationIds;
        private bool _configAutoDeclareQueue;
        private IRabbitMqConfig? _configRabbitMqConfig;
        private IQueueResolver? _configQueueResolver;
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
                _configUseStronglyTypedMessages = true;

                return this;
            }
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
        /// If set, consumers will automatically declare the configured queue in <see cref="IRabbitMqConfig"/>.
        /// </summary>
        public RabbitMqServiceBuilder<T> AutoDeclareQueue
        {
            get
            {
                _configAutoDeclareQueue = true;

                return this;
            }
        }

        /// <summary>
        /// Sets the <see cref="IRabbitMqConfig"/> for the registered <see cref="RabbitMqService{T}"/>.
        /// </summary>
        public RabbitMqServiceBuilder<T> WithRabbitMqConfig(IRabbitMqConfig rabbitMqConfig)
        {
            _configRabbitMqConfig = rabbitMqConfig ?? new RabbitMqConfig();

            return this;
        }

        /// <summary>
        /// Sets the <see cref="IQueueResolver"/> for the registered <see cref="RabbitMqService{T}"/>.
        /// </summary>
        public RabbitMqServiceBuilder<T> WithQueueResolver(IQueueResolver queueResolver)
        {
            _configQueueResolver = queueResolver;

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
        /// Registers a subclass of <see cref="RabbitMqService{T}"/> to be used with dependency injection.
        ///
        /// If it is a consumer with type <c>TConsumer</c>, it is registered both as
        /// <see cref="RabbitMqConsumer{TConsumer}"/> and <c>TConsumer</c>.
        ///
        /// If it is a producer with type <c>TProducer</c>, it is registered both as
        /// <see cref="RabbitMqProducer{TProducer}"/> and <c>TProducer</c>.
        /// </summary>
        /// <remarks>
        /// It reuses an existing <see cref="IBusProxy"/> singleton, if one is found using the provided
        /// <see cref="IRabbitMqConfig.Id"/>.
        ///
        /// If not, a new <see cref="IBusProxy"/> singleton is registered.
        /// </remarks>
        public void Add(IServiceCollection serviceCollection)
        {
            var isConsumer = typeof(T).IsSubclassOf(typeof(RabbitMqConsumer<T>));

            if (!isConsumer && !typeof(T).IsSubclassOf(typeof(RabbitMqProducer<T>)))
            {
                throw new Exception(
                    $"{nameof(T)}, of type {typeof(T).FullName}, must be a subclass of " +
                    $"{nameof(RabbitMqConsumer<T>)} or {nameof(RabbitMqProducer<T>)}.");
            }

            _configRabbitMqConfig ??= new RabbitMqConfig();

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
                            ValidateScopes = false,
                            ValidateOnBuild = false
                        })
                        .CreateBuilder(serviceCollection)
                        .BuildServiceProvider();

                var bus = RabbitMqService<T>.CreateLazyBus(
                    _configRabbitMqConfig, _configUseStronglyTypedMessages, _configUseCorrelationIds, serviceProvider);

                busProxy = new BusProxy(_configRabbitMqConfig.Id, bus);

                serviceCollection.AddSingleton(busProxy);
            }
            // ReSharper restore HeuristicUnreachableCode

            if (isConsumer)
            {
                if (_configRabbitMqConfig.Queue != null)
                {
                    if (string.IsNullOrWhiteSpace(_configRabbitMqConfig.Queue.Name))
                    {
                        throw new Exception(
                            $"The {nameof(IRabbitMqConfig.Queue.Name)} must not be null, blank or " +
                            $"whitespace when a non null {nameof(IRabbitMqConfig.Queue)} has been configured.");
                    }
                }
                else if (_configQueueResolver == null)
                {
                    throw new Exception(
                        $"When {nameof(IRabbitMqConfig.Queue)} has been configured as null, then a non null " +
                        $"{nameof(IQueueResolver)} must be provided.");
                }

                if (_configQueueResolver != null)
                {
                    serviceCollection.Add(
                        new ServiceDescriptor(
                            typeof(IQueueResolver<T>), _ => _configQueueResolver, ServiceLifetime.Singleton));
                }

                if (_configAutoDeclareQueue)
                {
                    OnConnected((bus, rabbitMqConfig, cancellationToken, logger) =>
                    {
                        Debug.Assert(
                            rabbitMqConfig != null,
                            $"Null {nameof(IRabbitMqConfig)} while trying to auto-declare the consumer's queue.");

                        Debug.Assert(
                            rabbitMqConfig.Queue != null,
                            $"Null {nameof(IRabbitMqConfig.Queue)} while trying to auto-declare the consumer's queue.");

                        logger?.LogDebug(
                            $"Declaring queue \"{rabbitMqConfig.Queue.Name}\" (Id = {rabbitMqConfig.Id}, " +
                            $"{rabbitMqConfig.Queue}).");

                        var queue = bus.QueueDeclare(rabbitMqConfig.Queue.Name, config =>
                        {
                            config.AsDurable(rabbitMqConfig.Queue.Durable);
                            config.AsExclusive(rabbitMqConfig.Queue.DeclareExclusive);
                            config.AsAutoDelete(rabbitMqConfig.Queue.AutoDelete);
                        }, cancellationToken);

                        rabbitMqConfig.DeclaredQueue = queue;
                    });
                }
            }

            Func<IServiceProvider, T> ServiceFactoryFactory()
            {
                T service = null!;
                var @lock = new object();

                return serviceProvider =>
                {
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
                            isConsumer, busProxy, _configRabbitMqConfig!, _configOnConnected, serviceProvider);

                        return service;
                    }
                };
            }

            var serviceFactory = ServiceFactoryFactory();

            serviceCollection.AddHostedService(serviceFactory);

            var serviceType = isConsumer ? typeof(RabbitMqConsumer<T>) : typeof(RabbitMqProducer<T>);

            // enable injection as RabbitMqConsumer<MyConsumerType> or RabbitMqProducer<MyProducerType>
            serviceCollection.AddSingleton(serviceType, serviceFactory);

            // enable injection as MyConsumerType or MyProducerType
            serviceCollection.AddSingleton(serviceFactory);
        }
    }
}
