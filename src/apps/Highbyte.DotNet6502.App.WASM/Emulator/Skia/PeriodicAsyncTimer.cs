using System.Diagnostics;

namespace Highbyte.DotNet6502.App.WASM.Emulator.Skia;

/// <summary>
/// Periodic timer for the WASM emulator frame loop.
///
/// .NET's <see cref="PeriodicTimer"/> rounds intervals to whole milliseconds, which makes
/// non-integer intervals (e.g. C64 NTSC at 16.7151ms) fire ~4-5% too fast on average. The
/// browser also clamps timer resolution further. This class polls at half the target interval
/// and uses a wall-clock accumulator to fire Elapsed 0/1/2 times per OS tick so the long-term
/// average matches IntervalMilliseconds. Sleep+spin is not viable on the single browser thread.
/// </summary>
public class PeriodicAsyncTimer
{
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastTick;

    public double IntervalMilliseconds { get; set; }

    public long TimeSinceLastTickMilliseconds { get; private set; }

    public event EventHandler? Elapsed;

    public void Dispose()
    {
        _cts?.Dispose();
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _ = StartTimer();
    }

    private async Task StartTimer()
    {
        var intervalTicks = (long)(IntervalMilliseconds * Stopwatch.Frequency / 1000.0);
        if (intervalTicks <= 0)
            return;

        // Track the deadline in Stopwatch ticks for full precision. WASM's
        // Stopwatch.Elapsed.TotalMilliseconds rounds coarsely (Spectre clamps performance.now()
        // to >= 0.1ms, sometimes 1-5ms), which makes the "now >= deadline" check fire ~1ms
        // early per frame. Convert to ms only to ask Task.Delay how long to sleep. Use
        // Task.Yield for sub-ms drain to avoid the browser's ~4ms Task.Delay floor.
        var nextDeadlineTicks = Stopwatch.GetTimestamp() + intervalTicks;
        var ct = _cts!.Token;

        while (!ct.IsCancellationRequested)
        {
            long now;
            while ((now = Stopwatch.GetTimestamp()) < nextDeadlineTicks)
            {
                var remainingMs = (nextDeadlineTicks - now) * 1000.0 / Stopwatch.Frequency;
                try
                {
                    // Deliberately undershoot Task.Delay by 2ms; Task.Yield loop fine-grains
                    // the final ~2ms for ms-or-better precision without Task.Delay overshoot.
                    if (remainingMs >= 4.0)
                        await Task.Delay((int)remainingMs - 2, ct);
                    else
                        await Task.Yield();
                }
                catch (TaskCanceledException) { return; }
            }

            // Resync if we fell more than 5 frames behind (e.g. tab was backgrounded).
            if (now - nextDeadlineTicks > intervalTicks * 5)
                nextDeadlineTicks = now;

            nextDeadlineTicks += intervalTicks;

            var time = _stopwatch.ElapsedMilliseconds;
            TimeSinceLastTickMilliseconds = time - _lastTick;
            _lastTick = time;
            Elapsed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
    }
}
