# EasyNetQ.HostedService

A wrapper around EasyNetQ (https://easynetq.com/) to provide an easy API for a .NET Core hosted service.

## Example usage

#### 1. Create consumer and producer types

###### Example message type

```c#
namespace EasyNetQ.HostedService.TestApp
{
    public struct EchoMessage
    {
        public string Text { get; set; }
    }
}
```

###### Example consumer

```c#
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AMLRecordsIndexer.RabbitMQ.Messages;
using EasyNetQ;
using EasyNetQ.HostedService;
using EasyNetQ.HostedService.Message.Abstractions;
using Microsoft.Extensions.Hosting;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace EasyNetQ.HostedService.TestApp
{
    public class MyInjectableRabbitMqConsumer : RabbitMqConsumer<MyInjectableRabbitMqConsumer>
    {
        // optional constructor with additional injected dependencies
        public void MyInjectableRabbitMqConsumer(IHostEnvironment env)
        {
            // do something with env
        }

        protected override IDictionary<Type, Func<IMessage, MessageReceivedInfo, CancellationToken, Task>>
            MessageHandlerMap =>
            new Dictionary<Type, Func<IMessage, MessageReceivedInfo, CancellationToken, Task>>
            {
                {
                    typeof(string),
                    ConsumerHandler.Wrap<string>((message, info, arg3) =>
                    {
                        var msg = System.Text.Json.JsonSerializer.Deserialize<EchoMessage>(message.Body, new JsonSerializerOptions()
                        {
                            PropertyNameCaseInsensitive = true,
                        });

                        Console.WriteLine($"Received simple message: {message.Body}");
                        Console.WriteLine($"Received simple deserialized message: {msg.Text}");

                        return Task.CompletedTask;
                    })
                }
            };

        protected override void Initialize()
        {
            // use initialized members like `Bus` and `RabbitMqConfig`
        }
    }
}
```

###### Example producer

```c#
using Microsoft.Extensions.Hosting;

namespace EasyNetQ.HostedService.TestApp
{
    public class MyInjectableRabbitMqProducer : RabbitMqProducer<MyInjectableRabbitMqProducer>
    {
        // optional constructor with additional injected dependencies
        public MyInjectableRabbitMqProducer(IHostEnvironment env)
        {
            // do something with env
        }

        protected override void Initialize()
        {
            // use initialized members like `Bus` and `RabbitMqConfig`
        }
    }
}
```

###### Example service using the example producer as a dependency

```c#
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyNetQ.HostedService.TestApp
{
    public class RabbitMqServiceTester : IHostedService
    {
        private readonly RabbitMqProducer<MyInjectableRabbitMqProducer> _producer;
        private readonly ILogger<RabbitMqServiceTester> _logger;

        // RabbitMqService<MyInjectableRabbitMqProducer> would work as well
        public RabbitMqServiceTester(MyInjectableRabbitMqProducer producer, ILogger<RabbitMqServiceTester> logger)
        {
            _producer = producer;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    var key = Console.ReadKey();

                    if (key.Key == ConsoleKey.Escape)
                    {
                        break;
                    }

                    await _producer.PublishAsync("", "test.queue", new EchoMessage {Text = "This is a test."});
                }

                Environment.Exit(0);
            }, cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
```

#### 2. Register hosted services with dependency injection

###### Example program using a .NET Core Generic Host

```c#
using System;
using EasyNetQ.HostedService.DependencyInjection;
using EasyNetQ.HostedService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EasyNetQ.HostedService.TestApp
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var rabbitMqConfigConsumer =
                        hostContext.Configuration.GetSection("RabbitMQ").Get<RabbitMqConfig>();

                    // rabbitMqConfigConsumer.Id can be set through the relevant configuration section
                    // (default value: Anonymous)

                    new RabbitMqServiceBuilder()
                        .WithRabbitMqConfig(rabbitMqConfigConsumer)
                        .AutoDeclareQueue
                        .Add<MyInjectableRabbitMqConsumer>(services);

                    var rabbitMqConfigProducer = rabbitMqConfigConsumer.Copy;
                    rabbitMqConfigProducer.Id = "Some Other Id";

                    new RabbitMqServiceBuilder()
                        .WithRabbitMqConfig(rabbitMqConfigProducer)
                        .Add<MyInjectableRabbitMqProducer>(services);

                    services.AddHostedService<RabbitMqServiceTester>();
                });
    }
}
```

## Verification of NuGet packages

To verify the integrity of the NuGet packages, checkout the git tag matching
the NuGet package's version and verify that the SHA512 hash of the NuGet
package matches the one in
`https://github.com/sadesyllas/EasyNetQ.HostedService/blob/master/package-hashes.txt`.

## Documentation

If you have `doxygen` installed, by running `doxygen` in the solution's
directory, the configuration `Doxyfile` is automatically used and the HTML
documentation for both `EasyNetQ.HostedService` and
`EasyNetQ.HostedService.Message.Abstractions` will become available in
directory `doc/html`.

