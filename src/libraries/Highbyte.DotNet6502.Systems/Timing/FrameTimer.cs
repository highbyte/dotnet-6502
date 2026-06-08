using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems.Timing;

/// <summary>
/// High-precision periodic timer for emulator frame loops.
///
/// .NET's <see cref="PeriodicTimer"/> rounds intervals to whole milliseconds, which makes
/// non-integer intervals (e.g. C64 NTSC at 16.7151ms) fire ~4-5% too fast on average. This
/// class compensates with two strategies, picked at runtime:
///
/// - Desktop (and any non-browser host): a Stopwatch-paced sleep+spin loop on a background
///   task. Sleeps while far from the deadline, spins for the final sub-millisecond. Gives
///   ~0.1ms accuracy on the long-term average.
///
/// - Browser/WASM (single-threaded): an absolute-deadline loop that deliberately undershoots
///   <see cref="Task.Delay"/> by 2ms (since browsers clamp Task.Delay to ~4ms minimum), then
///   uses a <see cref="Task.Yield"/> drain loop for the final ~2ms. Deadlines are tracked in
///   Stopwatch ticks (not Elapsed.TotalMilliseconds, which WASM rounds coarsely). Deadlines
///   never reset to "now" - they advance by exactly intervalMs per fired frame, so any sleep
///   overshoot is repaid next iteration.
///
/// Hosts that need to marshal Elapsed onto a UI thread pass a marshal callback to the
/// constructor; null fires Elapsed synchronously on the timer thread.
/// </summary>
public class FrameTimer : IScriptingTickTimer, IAsyncDisposable
{
    private readonly Func<Action, Task>? _marshal;
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

    /// <param name="marshal">
    /// Optional. Called to invoke the Elapsed handler on a specific thread (e.g. an Avalonia
    /// or Blazor UI thread). When null, Elapsed fires synchronously on the timer thread.
    /// On Avalonia, pass <c>action =&gt; Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Render).GetTask()</c>.
    /// Awaiting the returned Task back-pressures the timer so frames don't queue up.
    /// </param>
    public FrameTimer(Func<Action, Task>? marshal = null)
    {
        _marshal = marshal;
    }

    public void Start()
    {
        Stop();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (OperatingSystem.IsBrowser())
        {
            // Browser is single-threaded; run on the current synchronization context (UI thread).
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
                while ((now = Stopwatch.GetTimestamp()) < nextDeadline && !ct.IsCancellationRequested)
                {
                    var remainingMs = (nextDeadline - now) * 1000.0 / Stopwatch.Frequency;
                    if (remainingMs > 2.0)
                        // No cancellation token passed to Task.Delay on purpose: cancelling a
                        // short delay throws TaskCanceledException, which under a debugger spams
                        // first-chance exceptions on every pause/stop. The IsCancellationRequested
                        // checks in both while-loops end the loop within ~1ms instead.
                        await Task.Delay(1).ConfigureAwait(false);
                    else
                        Thread.SpinWait(50);
                }

                // Schedule next tick relative to the previous deadline so timing doesn't drift.
                // If we fell more than one frame behind (e.g. process was suspended), resync.
                nextDeadline += intervalTicks;
                if (now - nextDeadline > intervalTicks)
                    nextDeadline = now + intervalTicks;

                await FireElapsedAsync().ConfigureAwait(false);
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

        var nextDeadlineTicks = Stopwatch.GetTimestamp() + intervalTicks;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                long now;
                while ((now = Stopwatch.GetTimestamp()) < nextDeadlineTicks && !ct.IsCancellationRequested)
                {
                    var remainingMs = (nextDeadlineTicks - now) * 1000.0 / Stopwatch.Frequency;
                    // Deliberately undershoot Task.Delay by 2ms - it can return slightly early
                    // or late, and browsers clamp small values. Then a Task.Yield loop drains
                    // the final ~2ms with ms-or-better precision.
                    // No cancellation token passed to Task.Delay on purpose: cancelling it throws
                    // TaskCanceledException (first-chance exception spam on every pause/stop). The
                    // IsCancellationRequested checks in both while-loops end the loop instead.
                    if (remainingMs >= 4.0)
                        await Task.Delay((int)remainingMs - 2).ConfigureAwait(true);
                    else
                        await Task.Yield();
                }

                // Resync if we fell more than 5 frames behind (e.g. tab was backgrounded).
                if (now - nextDeadlineTicks > intervalTicks * 5)
                    nextDeadlineTicks = now;

                nextDeadlineTicks += intervalTicks;
                await FireElapsedAsync().ConfigureAwait(true);
                // Browser hosts are single-threaded. If emulation work overruns the target interval,
                // FireElapsedAsync can complete synchronously and the loop would otherwise spin
                // without yielding, starving input/render processing.
                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    private Task FireElapsedAsync()
    {
        var time = _stopwatch.ElapsedMilliseconds;
        TimeSinceLastTickMilliseconds = time - _lastTick;
        _lastTick = time;

        if (_marshal != null)
            return _marshal(() => Elapsed?.Invoke(this, EventArgs.Empty));

        Elapsed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
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
