// ReSharper disable UnusedMember.Local

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EasyNetQ.HostedService.TestApp
{
    public class RabbitMqServiceTester : IHostedService
    {
        private readonly RabbitMqServiceTestConsumer _testConsumer;
        private readonly RabbitMqServiceTestProducer _testProducer;
        private readonly ILogger<RabbitMqServiceTester> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public RabbitMqServiceTester(
            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            IRabbitMqServiceTestConsumer iTestConsumer,
            RabbitMqServiceTestConsumer testConsumer,
            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            IRabbitMqServiceTestProducer iTestProducer,
            RabbitMqServiceTestProducer testProducer,
            ILogger<RabbitMqServiceTester> logger,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            Debug.Assert(ReferenceEquals(iTestConsumer, testConsumer),
                $"{nameof(iTestConsumer)} must be reference the same object as {nameof(testConsumer)}.");

            Debug.Assert(ReferenceEquals(iTestProducer, testProducer),
                $"{nameof(iTestProducer)} must be reference the same object as {nameof(testProducer)}.");

            _testConsumer = testConsumer;
            _testProducer = testProducer;
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting test...");

            RunProduceConsumeTest();

            // RunLoopBased(100_000, cancellationToken);

            // RunConsoleBased(cancellationToken).GetAwaiter().GetResult();

            _logger.LogDebug("Stopping application...");

            _hostApplicationLifetime.StopApplication();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void RunProduceConsumeTest()
        {
            const string message = "Hello World";

            // var payload = message;

            var payload = new EchoMessage {Text = message};

            _testProducer.PublishAsync("", Program.TestQueueName, payload).GetAwaiter().GetResult();

            // var response = _testConsumer.UntypedResponse.GetAwaiter().GetResult();
            //
            // _logger.LogDebug($"Received response: {response}");

            var response = _testConsumer.TypedResponse.GetAwaiter().GetResult();

            _logger.LogDebug($"Received response: {JsonConvert.SerializeObject(response)}");

            // var test = new string(message.Reverse().ToArray());

            // Debug.Assert(payload == test);

            var test = new string(response.Text.Reverse().ToArray());

            Debug.Assert(payload.Text == test);
        }

        private Task RunConsoleBased(CancellationToken cancellationToken) =>
            Task.Run(async () =>
            {
                while (true)
                {
                    var key = Console.ReadKey();

                    if (key.Key == ConsoleKey.Escape)
                    {
                        break;
                    }

                    await _testProducer.PublishAsync(
                        "",
                        Program.TestQueueName,
                        new EchoMessage {Text = "This is a test."});
                }
            }, cancellationToken);

        private void RunLoopBased(int max, CancellationToken cancellationToken)
        {
            Task.WaitAll(Enumerable.Range(0, max).Select(i => Task.Run(async () =>
            {
                _logger.LogDebug($"Sending message #{i}.");

                var result = await _testProducer
                    .PublishAsync(
                        "",
                        Program.TestQueueName,
                        new EchoMessage {Text = "This is a test."})
                    .ConfigureAwait(false);

                _logger.LogDebug($"Result #{i}: {result}.");
            }, cancellationToken)).ToArray());
        }
    }
}
