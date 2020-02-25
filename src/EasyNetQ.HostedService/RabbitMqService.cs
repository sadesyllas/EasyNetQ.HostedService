using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Consumer;
using EasyNetQ.HostedService.Abstractions;
using EasyNetQ.HostedService.DependencyInjection;
using EasyNetQ.HostedService.Internals;
using EasyNetQ.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EasyNetQ.HostedService
{
    /// <summary>
    /// The callback type for callbacks that must run when a new connection is established to the RabbitMQ server.
    /// </summary>
    /// <param name="b"/>
    /// <param name="c"/>
    /// <param name="t"/>
    /// <param name="logger"/>
    public delegate void OnConnectedCallback(IAdvancedBus b, IRabbitMqConfig c, CancellationToken t, ILogger? logger);

    /// <summary>
    /// A hosted service that accepts an EasyNetQ <see cref="IAdvancedBus"/> and uses it to either set up a consumer or
    /// a producer, given the configuration with which it has been created.
    /// </summary>
    /// <typeparam name="T">
    /// The subclass of <c>RabbitMqService&lt;T&gt;</c> that will be registered as either a consumer or a producer
    /// through dependency injection.
    /// </typeparam>
    /// <remarks>
    /// The purpose of this class is to gather the common concerns of handling an <see cref="IAdvancedBus"/> into a
    /// central place.
    ///
    /// There are two provided, abstract derived classes, <see cref="RabbitMqConsumer{T}"/> and
    /// <see cref="RabbitMqProducer{T}"/>.
    ///
    /// The types that are to be registered with dependency injection, as consumers or producers, should derive one
    /// of these classes and potentially override the base class's functionality, as required.
    ///
    /// Any required services can be injected through constructor parameters, in the derived class,
    /// as with any other hosted service.
    ///
    /// For details and examples of the provided consumer and producer subclasses, please,
    /// see <see cref="RabbitMqConsumer{T}"/> and <see cref="RabbitMqProducer{T}"/>.
    /// </remarks>
    public abstract class RabbitMqService<T> : IHostedService
    {
        private bool _isConsumer;
        private bool _isProperlyInitialized;
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private ILogger? _logger;

        // ReSharper disable RedundantDefaultMemberInitializer

        private IBusProxy _busProxy = null!;
        private IRabbitMqConfig _rmqConfig = null!;
        private List<OnConnectedCallback> _onConnected = null!;

        // ReSharper restore RedundantDefaultMemberInitializer

        /// <summary>
        /// This property must be implemented by consumer types so that the service knows how to handle each message
        /// by type.
        ///
        /// If using typed messages (see <see cref="RabbitMqServiceBuilder{T}.WithStronglyTypedMessages"/>), then a
        /// handler should be provided for each expected message type.
        ///
        /// If not using typed messages, then only a handler for the <see cref="string"/> should be registered.
        ///
        /// In any case, a handler for the <see cref="object"/> type is always registered, if not already registered,
        /// as a fallback which throws an <see cref="UnhandledMessageTypeException"/>.
        /// </summary>
        protected abstract IDictionary<Type, Func<IMessage, MessageReceivedInfo, CancellationToken, Task>>
            MessageHandlerMap { get; }

        /// <summary>
        /// The initialized <see cref="IAdvancedBus"/> that is exposed to subclasses of
        /// <see cref="RabbitMqService{T}"/>.
        /// </summary>
        protected IAdvancedBus Bus => _busProxy.Bus;

        /// <summary>
        /// The populated <see cref="IRabbitMqConfig"/> that is exposed to subclasses of
        /// <see cref="RabbitMqService{T}"/>.
        /// </summary>
        protected IRabbitMqConfig RabbitMqConfig => _rmqConfig;

        /// <summary>
        /// The initialized <see cref="ILogger{T}"/> that is exposed to subclasses of <see cref="RabbitMqService{T}"/>.
        /// </summary>
        protected ILogger? Logger => _logger;

        static RabbitMqService()
        {
            // disable the default log provider of EasyNetQ since we have not (obvious) way to apply log filtering to it
            LogProvider.IsDisabled = true;
        }

        /// <summary>
        /// This static method is used by <see cref="RabbitMqServiceBuilder{T}"/> to construct a singleton hosted service
        /// for a consumer of a producer.
        ///
        /// Its purpose is to instantiate the <c>TDerived</c>, passing in the <see cref="IServiceProvider"/> argument.
        ///
        /// The services that can be injected this way are the ones that have been registered up to the call site of
        /// <see cref="Create{TDerived}"/>.
        /// </summary>
        /// <param name="isConsumer"/>
        /// <param name="busProxy"/>
        /// <param name="rmqConfig"/>
        /// <param name="onConnected"/>
        /// <param name="serviceProvider"/>
        /// <typeparam name="TDerived">
        /// The type of the consumer or producer that is being instantiated. It must match type parameter <c>T</c> of
        /// type <see cref="RabbitMqService{T}"/>.
        /// </typeparam>
        /// <returns>
        /// An instantiated <see cref="RabbitMqService{TDerived}"/> which should be used to register a consumer or
        /// producer as a singleton hosted service.
        /// </returns>
        public static TDerived Create<TDerived>(
            bool isConsumer,
            IBusProxy busProxy,
            IRabbitMqConfig rmqConfig,
            List<OnConnectedCallback> onConnected,
            IServiceProvider serviceProvider)
            where TDerived : RabbitMqService<TDerived>
        {
            Debug.Assert(busProxy?.Bus != null, $"{nameof(IBusProxy.Bus)} must not be null.");

            Debug.Assert(rmqConfig != null, $"{nameof(IRabbitMqConfig)} must not be null.");

            if (isConsumer)
            {
                if (rmqConfig.Queue == null)
                {
                    var queueResolver = (IQueueResolver) serviceProvider.GetService(typeof(IQueueResolver<TDerived>));

                    if (queueResolver != null)
                    {
                        rmqConfig.Queue = queueResolver.Queue;
                    }
                }

                Debug.Assert(rmqConfig.Queue != null, $"{nameof(IRabbitMqConfig.Queue)} must not be null.");

                Debug.Assert(
                    !string.IsNullOrWhiteSpace(rmqConfig.Queue.Name),
                    $"{nameof(IRabbitMqConfig.Queue.Name)} must not be null, blank or whitespace.");
            }

            var logger = serviceProvider.GetService<ILogger<TDerived>>();

            TDerived service;

            try
            {
                service = ActivatorUtilities.CreateInstance<TDerived>(serviceProvider);
            }
            catch (Exception)
            {
                // ReSharper disable once ConstantConditionalAccessQualifier
                logger?.LogCritical(
                    $"Could not instantiate a {typeof(TDerived).Name} in {nameof(RabbitMqService<T>)}." +
                    $"{nameof(Create)}.");

                throw;
            }

            service._isConsumer = isConsumer;
            service._busProxy = busProxy;
            service._rmqConfig = rmqConfig;
            service._onConnected = onConnected;
            service._logger = logger;
            service._isProperlyInitialized = true;

            service.Initialize();

            return service;
        }

        /// <summary>
        /// This static method is used by <see cref="RabbitMqServiceBuilder{T}"/> to construct a singleton hosted
        /// service for a consumer of a producer.
        ///
        /// Its purpose is to be used with <see cref="RabbitMqServiceBuilder{T}"/> so as to allow reuse of
        /// <see cref="IAdvancedBus"/> singletons
        ///
        /// This is done in order to enable the use of the same or different connections to the RabbitMQ server, on
        /// demand.
        ///
        /// For more information about how <see cref="IAdvancedBus"/> instances can be reused, see
        /// <see cref="RabbitMqServiceBuilder{T}"/>.
        /// </summary>
        /// <param name="rmqConfig"/>
        /// <param name="useStronglyTypedMessages"/>
        /// <param name="useCorrelationIds"/>
        /// <param name="serviceProvider"/>
        /// <returns/>
        public static Lazy<IAdvancedBus> CreateLazyBus(
            IRabbitMqConfig rmqConfig,
            bool useStronglyTypedMessages,
            bool useCorrelationIds,
            IServiceProvider serviceProvider) =>
            new Lazy<IAdvancedBus>(() => RabbitHutch.CreateBus(new ConnectionConfiguration
            {
                Hosts = new List<HostConfiguration>
                {
                    new HostConfiguration
                    {
                        Host = rmqConfig.HostName,
                        Port = rmqConfig.Port
                    }
                },
                Name = rmqConfig.Id,
                UserName = rmqConfig.UserName,
                Password = rmqConfig.Password,
                Product = nameof(RabbitMqService<T>),
                RequestedHeartbeat = rmqConfig.RequestedHeartbeatSeconds,
                PersistentMessages = rmqConfig.PersistentMessages,
                PublisherConfirms = rmqConfig.PublisherConfirms,
                Timeout = rmqConfig.MessageDeliveryTimeoutSeconds,
            }, container =>
            {
                container.Register(serviceProvider);

                container.Register<IConsumerErrorStrategy, ConsumerErrorStrategy>();

                container.Register<IMessageSerializationStrategy>(serviceResolver =>
                {
                    var serializer = serviceResolver.Resolve<ISerializer>();
                    var correlationIdGenerationStrategy = serviceResolver.Resolve<ICorrelationIdGenerationStrategy>();

                    if (useStronglyTypedMessages)
                    {
                        var typeNameSerializer = serviceResolver.Resolve<ITypeNameSerializer>();

                        return new TypedMessageSerializationStrategy(
                            useCorrelationIds, typeNameSerializer, serializer, correlationIdGenerationStrategy);
                    }

                    return new UntypedMessageSerializationStrategy(
                        useCorrelationIds, serializer, correlationIdGenerationStrategy);
                });
            }).Advanced);

        /// <summary>
        /// Starts the hosted service and initializes the consumer or producer by either setting up the consumption of
        /// messages or by starting the producer's message queueing functionality.
        ///
        /// For details on the default implementations of consumers and producers, see <see cref="RabbitMqConsumer{T}"/>
        /// and <see cref="RabbitMqProducer{T}"/>.
        /// </summary>
        /// <param name="cancellationToken"/>
        /// <returns/>
        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            Debug.Assert(
                _isProperlyInitialized,
                $"This {nameof(RabbitMqService<T>)} instance should have been created through a call to " +
                $"{nameof(RabbitMqService<T>)}.{nameof(Create)}.");

            _logger?.LogInformation($"RabbitMqService {_rmqConfig.Id} is starting.");

            if (Bus.IsConnected)
            {
                _logger?.LogDebug($"Connected to RabbitMQ with configuration {_rmqConfig.Id}.");
            }

            Bus.Connected += (sender, args) =>
                _logger?.LogDebug($"Connected to RabbitMQ with configuration {_rmqConfig.Id}.");

            Bus.Disconnected += (sender, args) =>
                _logger?.LogDebug($"Disconnected from RabbitMQ with configuration {_rmqConfig.Id}.");

            // run the setup callbacks on connection

            if (Bus.IsConnected)
            {
                _onConnected.ForEach(callback =>
                    HandleCallbackError(callback)(Bus, _rmqConfig, cancellationToken, _logger));
            }

            _onConnected.ForEach(callback => Bus.Connected += (sender, args) =>
                HandleCallbackError(callback)(Bus, _rmqConfig, cancellationToken, _logger));

            // if this is a consumer, then start consuming

            if (_isConsumer)
            {
                InitializeConsumer(cancellationToken);
            }
            else
            {
                InitializeProducer(cancellationToken);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the hosted service by first disposing of any <c>IDisposable</c> instances that are created during
        /// service startup.
        /// </summary>
        /// <param name="cancellationToken"/>
        /// <returns/>
        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation($"RabbitMqService {_rmqConfig.Id} is stopping.");

            if (!cancellationToken.IsCancellationRequested)
            {
                var serviceTypeName = _isConsumer ? nameof(RabbitMqConsumer<T>) : nameof(RabbitMqProducer<T>);

                try
                {
                    _disposables.ForEach(subscription => subscription.Dispose());
                }
                catch (Exception exception)
                {
                    _logger?.LogCritical(
                        $"Duplicate IDisposable disposal on {serviceTypeName} termination: " +
                        $"{exception.Message}\n" +
                        exception.StackTrace);
                }

                try
                {
                    Bus.Dispose();
                }
                catch (Exception exception)
                {
                    _logger?.LogCritical(
                        $"Could not dispose {nameof(IAdvancedBus)} on {serviceTypeName} termination: " +
                        $"{exception.Message}\n" +
                        exception.StackTrace);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// This virtual function is called right after the instantiation of the subclass of
        /// <see cref="RabbitMqService{T}"/>.
        /// </summary>
        /// <remarks>
        /// Overriding this function allows the subclass of <see cref="RabbitMqService{T}"/> to access fields that are
        /// initialized after the construction of the instance, eg, <see cref="RabbitMqService{T}"/>.
        /// <see cref="RabbitMqConfig"/>.
        /// </remarks>
        protected virtual void Initialize()
        {
        }

        /// <summary>
        /// This abstract function must be implemented by a subclass in order to initialize a consumer.
        ///
        /// This is not called for producers and it's declared as abstract only to enforce its implementation by
        /// consumers.
        /// </summary>
        /// <param name="cancellationToken"/>
        protected internal abstract void InitializeConsumer(CancellationToken cancellationToken);

        /// <summary>
        /// This abstract function must be implemented by a subclass in order to initialize a producer.
        ///
        /// This is not called for consumers and it's declared as abstract only to enforce its implementation by
        /// producers.
        /// </summary>
        /// <param name="cancellationToken"/>
        protected internal abstract void InitializeProducer(CancellationToken cancellationToken);

        /// <summary>
        /// Adds a disposable into a common list of disposables in order to dispose of it on service termination.
        /// </summary>
        /// <param name="disposable"/>
        protected void AddDisposable(IDisposable disposable) => _disposables.Add(disposable);

        private OnConnectedCallback HandleCallbackError(OnConnectedCallback callback) =>
            (bus, rabbitMqConfig, cancellationToken, logger) =>
            {
                try
                {
                    callback(bus, rabbitMqConfig, cancellationToken, logger);
                }
                catch (Exception exception)
                {
                    _logger?.LogCritical($"Failed to run callback on connection:\n{exception}", exception);
                }
            };
    }
}
