using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Highbyte.DotNet6502.Systems.Timing;

namespace Highbyte.DotNet6502.Systems.Tests;

public class BrowserFrameTimerTests
{
    [Fact]
    public async Task OverrunningFramesLetTheUiDispatcherReturnToTheBrowser()
    {
        using var cancellation = new CancellationTokenSource();
        using var timer = new FrameTimer { IntervalMilliseconds = 1 };
        var context = new QueuedUiContext();
        var frames = 0;
        timer.Elapsed += (_, _) =>
        {
            frames++;
            // Model a Debug emulator frame taking longer than its target interval.
            var end = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 500;
            while (Stopwatch.GetTimestamp() < end)
                Thread.SpinWait(64);
        };
        // Exercise the actual browser loop on a desktop test host. No browser or
        // public platform-selection override is needed for this scheduling test.
        var method = typeof(FrameTimer).GetMethod("RunBrowserAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var previousContext = SynchronizationContext.Current;
        Task run;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            run = (Task)method.Invoke(timer, [cancellation.Token])!;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        try
        {
            // Let the initial deadline pass before draining Avalonia-like posted jobs.
            await Task.Delay(10);
            var callbacks = 0;
            while (callbacks < 25 && context.PumpOne())
                callbacks++;
            Assert.True(frames > 0);
            Assert.True(callbacks < 25,
                "The UI dispatcher never became idle: overrunning frames continually posted more work.");
        }
        finally
        {
            await cancellation.CancelAsync();
            var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
            while (!run.IsCompleted && Stopwatch.GetTimestamp() < deadline)
            {
                context.PumpOne();
                await Task.Delay(1);
            }
            Assert.True(run.IsCompleted, "Cancelled browser timer did not exit.");
            await run;
        }
    }

    private sealed class QueuedUiContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _jobs = new();
        public override void Post(SendOrPostCallback callback, object? state) => _jobs.Enqueue((callback, state));

        public bool PumpOne()
        {
            if (!_jobs.TryDequeue(out var job))
                return false;
            var previous = Current;
            try
            {
                SetSynchronizationContext(this);
                job.Callback(job.State);
                return true;
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }
}
