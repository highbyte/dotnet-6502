using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Periodic timer for the emulator frame loop.
///
/// .NET's <see cref="PeriodicTimer"/> rounds intervals to whole milliseconds, which makes
/// non-integer intervals (e.g. C64 NTSC at 16.7151ms) fire ~4-5% too fast on average. This
/// class compensates with two strategies, picked at runtime:
///
/// - Desktop (and any non-browser host): a Stopwatch-paced sleep+spin loop on a background
///   task. Sleeps while far from the deadline, spins for the final sub-millisecond. Gives
///   accurate fractional intervals (~0.1ms accuracy on average).
///
/// - Browser/WASM (single-threaded): an absolute-deadline loop driven by <see cref="Task.Delay"/>
///   for big waits and <see cref="Task.Yield"/> for the final sub-ms drain. Deadlines are
///   tracked in Stopwatch ticks (not <see cref="Stopwatch.Elapsed"/>.TotalMilliseconds, which
///   WASM rounds coarsely). The deadline never resets to "now" - it advances by exactly
///   intervalMs per fired frame, so any sleep overshoot is repaid next iteration.
///
/// In both cases, Elapsed is invoked on the Avalonia UI thread.
/// </summary>
public class PeriodicAsyncTimer : IScriptingTickTimer, IAsyncDisposable
{
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastTick;

    public double IntervalMilliseconds { get; set; }
    public long TimeSinceLastTickMilliseconds { get; private set; }

    public event EventHandler? Elapsed;

    event EventHandler IScriptingTickTimer.Elapsed
    {
        add => Elapsed += value;
        remove => Elapsed -= value;
    }

    public void Start()
    {
        Stop();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (OperatingSystem.IsBrowser())
        {
            // Browser is single-threaded; run on current (UI) thread synchronization context.
            _ = RunBrowserAsync(token);
        }
        else
        {
            _ = Task.Run(() => RunDesktopAsync(token));
        }
    }

    private async Task RunDesktopAsync(CancellationToken ct)
    {
        var intervalTicks = (long)(IntervalMilliseconds * Stopwatch.Frequency / 1000.0);
        if (intervalTicks <= 0)
            return;

        var nextDeadline = Stopwatch.GetTimestamp() + intervalTicks;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                long now;
                while ((now = Stopwatch.GetTimestamp()) < nextDeadline)
                {
                    var remainingMs = (nextDeadline - now) * 1000.0 / Stopwatch.Frequency;
                    if (remainingMs > 2.0)
                        await Task.Delay(1, ct).ConfigureAwait(false);
                    else
                        Thread.SpinWait(50);
                }

                // Schedule next tick relative to the previous deadline so timing doesn't drift.
                // If we fell more than one frame behind (e.g. process was suspended), resync.
                nextDeadline += intervalTicks;
                if (now - nextDeadline > intervalTicks)
                    nextDeadline = now + intervalTicks;

                await FireElapsedOnUIThreadAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    private async Task RunBrowserAsync(CancellationToken ct)
    {
        var intervalTicks = (long)(IntervalMilliseconds * Stopwatch.Frequency / 1000.0);
        if (intervalTicks <= 0)
            return;

        // Track the deadline as raw Stopwatch ticks. WASM's Stopwatch.Elapsed.TotalMilliseconds
        // can round to coarse granularity (Spectre clamps performance.now() to >=0.1ms, sometimes
        // 1-5ms), which makes the "now >= deadline" check fire up to ~1ms early per frame. Using
        // ticks throughout keeps full precision; we convert to ms only to ask Task.Delay how long
        // to sleep. The deadline still advances by exactly intervalTicks each fire, so overshoot
        // is repaid by a shorter sleep next iteration.
        //
        // After Task.Delay returns, Task.Yield drains any final sub-ms slack without re-incurring
        // the browser's ~4ms Task.Delay floor.
        var nextDeadlineTicks = Stopwatch.GetTimestamp() + intervalTicks;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                long now;
                while ((now = Stopwatch.GetTimestamp()) < nextDeadlineTicks)
                {
                    var remainingMs = (nextDeadlineTicks - now) * 1000.0 / Stopwatch.Frequency;
                    // Deliberately undershoot Task.Delay by 2ms. Browser timers can return slightly
                    // early or late; undershooting guarantees we wake up before the deadline, and
                    // then a Task.Yield loop fine-grains the final ~2ms - that gives ms-or-better
                    // precision without the ~4ms Task.Delay clamp overshooting the deadline.
                    if (remainingMs >= 4.0)
                        await Task.Delay((int)remainingMs - 2, ct).ConfigureAwait(true);
                    else
                        await Task.Yield();
                }

                // Resync if we fell more than 5 frames behind (e.g. tab was backgrounded).
                if (now - nextDeadlineTicks > intervalTicks * 5)
                    nextDeadlineTicks = now;

                nextDeadlineTicks += intervalTicks;
                await FireElapsedOnUIThreadAsync().ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    private Task FireElapsedOnUIThreadAsync()
    {
        var time = _stopwatch.ElapsedMilliseconds;
        TimeSinceLastTickMilliseconds = time - _lastTick;
        _lastTick = time;

        // InvokeAsync runs the action synchronously when the caller is already on the UI thread
        // (browser path) and marshals from a worker thread otherwise (desktop path). Awaiting it
        // also back-pressures the desktop loop so we never queue overlapping frames.
        return Dispatcher.UIThread.InvokeAsync(
            () => Elapsed?.Invoke(this, EventArgs.Empty),
            DispatcherPriority.Render).GetTask();
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        Stop();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
