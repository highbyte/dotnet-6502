using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// A timer that uses .NET built-in PeriodicTimer.
/// As PeriodicTimer runs on the thread it is created on, we need to marshal the Elapsed event to the UI thread.
/// </summary>
public class PeriodicAsyncTimer : IDisposable, IAsyncDisposable
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
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ = StartTimer();
    }

    private async Task StartTimer()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(IntervalMilliseconds));

        try
        {
            while (await timer.WaitForNextTickAsync(_cts!.Token))
            {
                var time = _stopwatch.ElapsedMilliseconds;
                TimeSinceLastTickMilliseconds = time - _lastTick;
                _lastTick = time;

                // Marshal to UI thread for Avalonia
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Elapsed?.Invoke(this, EventArgs.Empty);
                }, DispatcherPriority.Render);
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
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
