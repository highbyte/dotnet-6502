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
        var intervalMs = IntervalMilliseconds;
        if (intervalMs <= 0)
            return;

        var pollMs = Math.Max(1.0, intervalMs / 2.0);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(pollMs));
        var sw = Stopwatch.StartNew();
        var lastNowMs = sw.Elapsed.TotalMilliseconds;
        var accumulatedMs = 0.0;

        while (await timer.WaitForNextTickAsync(_cts!.Token))
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

                var time = _stopwatch.ElapsedMilliseconds;
                TimeSinceLastTickMilliseconds = time - _lastTick;
                _lastTick = time;
                Elapsed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
    }
}
