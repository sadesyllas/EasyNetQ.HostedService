// ReSharper disable UnusedMember.Local

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.HostedService.Tracing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyNetQ.HostedService.TestApp
{
    public class TraceTestListener : IHostedService
    {
        private readonly ILogger<TraceTestListener> _logger;

        public TraceTestListener(ILogger<TraceTestListener> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            TraceLogListener.SubscribeToAll(log =>
            {
                _logger.Log(log.LogLevel, message: $"[DIAGNOSTIC EVENT] {log.Message}", exception: log.Exception);
            });

            TraceLogListener.Subscribe<RabbitMqServiceTestConsumer>(
                log =>
                {
                    _logger.Log(log.LogLevel, message: $"[DIAGNOSTIC EVENT - TEST CONSUMER] {log.Message}",
                        exception: log.Exception);
                }, LogLevel.Information);

            TraceLogListener.Subscribe<RabbitMqServiceTestProducer>(
                log =>
                {
                    _logger.Log(log.LogLevel, message: $"[DIAGNOSTIC EVENT - TEST PRODUCER] {log.Message}",
                        exception: log.Exception);
                }, LogLevel.Critical);

            TraceActivityListener.SubscribeToAll(
                activity => _logger.LogDebug(
                    $"ActivityStarted: id={activity.Id}, type={activity.Kind}, " +
                    $"events={activity.Events.Count()}, tags={activity.Tags.Count()}"),
                activity =>
                {
                    var log =
                        $"ActivityStopped: id={activity.Id}, type={activity.Kind}, " +
                        $"events={activity.Events.Count()}, tags={activity.Tags.Count()}";

                    if (activity.Tags.Any())
                    {
                        var tags = string.Join(',', activity.Tags.Aggregate(new List<string>(), (acc, val) =>
                        {
                            acc.Add($"{val.Key}={val.Value}");

                            return acc;
                        }));

                        _logger.LogDebug($"{log}\n\t[{tags}]");
                    }
                });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
