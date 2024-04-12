using System.Diagnostics;

namespace Highbyte.DotNet6502.App.WASM.Emulator.Skia;

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
        throw new NotImplementedException();
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _ = StartTimer();
    }

    private async Task StartTimer()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(IntervalMilliseconds));
        while (await timer.WaitForNextTickAsync(_cts!.Token))
        {
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
