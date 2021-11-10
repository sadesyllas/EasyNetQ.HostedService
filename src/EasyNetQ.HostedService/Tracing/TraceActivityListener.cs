using System;
using System.Diagnostics;

namespace EasyNetQ.HostedService.Tracing
{
    /// <summary>
    /// A convenience type to help register a handler for <see cref="Activity"/> start/end events, emitted by the
    /// library.
    /// </summary>
    public static class TraceActivityListener
    {
        /// <summary>
        /// Subscribes handlers for the <see cref="Activity"/> start/end events, emitted
        /// by an <see cref="ActivitySource"/>.
        /// </summary>
        /// <param name="onActivityStarted"></param>
        /// <param name="onActivityStopped"></param>
        public static void Subscribe<T>(Action<Activity> onActivityStarted, Action<Activity> onActivityStopped) =>
            ActivitySource.AddActivityListener(new ActivityListener
            {
                ShouldListenTo = source => source.Name == TraceSourceName<T>.Activity,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = onActivityStarted,
                ActivityStopped = onActivityStopped
            });

        /// <summary>
        /// Subscribes handlers for the <see cref="Activity"/> start/end events, emitted
        /// by an <see cref="ActivitySource"/>.
        ///
        /// This variant subscribes to all <see cref="Activity"/> start/end events, irrespective from the actual source
        /// within the library.
        /// </summary>
        /// <param name="onActivityStarted"></param>
        /// <param name="onActivityStopped"></param>
        public static void SubscribeToAll(Action<Activity> onActivityStarted, Action<Activity> onActivityStopped) =>
            ActivitySource.AddActivityListener(new ActivityListener
            {
                ShouldListenTo = source => source.Name.StartsWith(TraceSourceName.KeyPrefix),
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = onActivityStarted,
                ActivityStopped = onActivityStopped
            });
    }
}
