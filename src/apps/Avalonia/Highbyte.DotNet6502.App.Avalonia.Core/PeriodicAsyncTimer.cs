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
/// - Browser/WASM (single-threaded): a coarse <see cref="PeriodicTimer"/> polling at half the
///   target interval, with a wall-clock accumulator that fires Elapsed 0/1/2 times per OS
///   tick to track the requested rate on average. Sleep+spin would block the UI thread.
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
        var intervalMs = IntervalMilliseconds;
        if (intervalMs <= 0)
            return;

        // Poll at roughly half the target interval so the accumulator can fire on time.
        // Browsers clamp PeriodicTimer to ~4ms in foreground tabs.
        var pollMs = Math.Max(1.0, intervalMs / 2.0);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(pollMs));
        var sw = Stopwatch.StartNew();
        var lastNowMs = sw.Elapsed.TotalMilliseconds;
        var accumulatedMs = 0.0;

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(true))
            {
                var nowMs = sw.Elapsed.TotalMilliseconds;
                accumulatedMs += nowMs - lastNowMs;
                lastNowMs = nowMs;

                // Cap accumulation to avoid burst catch-up after a tab suspension or long stall.
                if (accumulatedMs > intervalMs * 5.0)
                    accumulatedMs = intervalMs;

                while (accumulatedMs >= intervalMs)
                {
                    accumulatedMs -= intervalMs;
                    await FireElapsedOnUIThreadAsync().ConfigureAwait(true);
                }
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
