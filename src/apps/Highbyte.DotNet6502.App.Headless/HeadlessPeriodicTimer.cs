using System.Diagnostics;

namespace Highbyte.DotNet6502.App.Headless;

/// <summary>
/// A periodic timer for headless (no-UI) execution.
/// Uses .NET <see cref="PeriodicTimer"/> without any UI-thread marshaling.
/// </summary>
public sealed class HeadlessPeriodicTimer : IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastTick;

    public double IntervalMilliseconds { get; set; }
    public long TimeSinceLastTickMilliseconds { get; private set; }

    public event EventHandler? Elapsed;

    public void Start()
    {
        Stop();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ = RunLoop();
    }

    private async Task RunLoop()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(IntervalMilliseconds));
        try
        {
            while (await timer.WaitForNextTickAsync(_cts!.Token))
            {
                var time = _stopwatch.ElapsedMilliseconds;
                TimeSinceLastTickMilliseconds = time - _lastTick;
                _lastTick = time;
                Elapsed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        finally
        {
            timer.Dispose();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
