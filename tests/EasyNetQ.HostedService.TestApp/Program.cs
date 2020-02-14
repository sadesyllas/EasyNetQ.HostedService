using System;
using EasyNetQ.HostedService.Abstractions;
using EasyNetQ.HostedService.DependencyInjection;
using EasyNetQ.HostedService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EasyNetQ.HostedService.TestApp
{
    public static class Program
    {
        internal static string TestQueueName;

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
                Log.Error($"Host crashed: {((Exception) eventArgs.ExceptionObject).Message}");

            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    TestQueueName = hostContext.Configuration.GetValue<string>("RabbitMQ:QueueName");

                    var rabbitMqConfigConsumer =
                        hostContext.Configuration.GetSection("RabbitMQ:Test").Get<RabbitMqConfig>();

                    new RabbitMqServiceBuilder<RabbitMqServiceTestConsumer>()
                        .WithRabbitMqConfig(rabbitMqConfigConsumer)
                        .WithStronglyTypedMessages
                        .Build(services)
                        .Add(services, typeof(RabbitMqServiceTestConsumer), typeof(IRabbitMqServiceTestConsumer));

                    var rabbitMqConfigProducer =
                        hostContext.Configuration.GetSection("RabbitMQ:Test").Get<RabbitMqConfig>();

                    rabbitMqConfigProducer.Id = "Producer";

                    new RabbitMqServiceBuilder<RabbitMqServiceTestProducer>()
                        .WithRabbitMqConfig(rabbitMqConfigProducer)
                        .WithStronglyTypedMessages
                        .Build(services)
                        .Add(services, typeof(RabbitMqServiceTestProducer), typeof(IRabbitMqServiceTestProducer));

                    services.AddSingleton<IRabbitMqConfig>(rabbitMqConfigConsumer);

                    services.AddHostedService<RabbitMqServiceTester>();

                    services
                        .AddSingleton<IIncomingMessageInterceptor<RabbitMqServiceTestConsumer>, MessageInterceptor>();

                    services
                        .AddSingleton<IOutgoingMessageInterceptor<RabbitMqServiceTestProducer>, MessageInterceptor>();
                })
                .UseSerilog();
    }
}
