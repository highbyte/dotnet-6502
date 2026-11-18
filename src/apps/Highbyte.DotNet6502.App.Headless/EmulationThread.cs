using System.Collections.Concurrent;

namespace Highbyte.DotNet6502.App.Headless;

/// <summary>
/// Runs frame ticks, scripting ticks, and host commands on one thread, serving the same
/// role as Avalonia's UI dispatcher.
/// </summary>
/// <remarks>
/// Headless timers and remote commands originate on different threads. Queuing their
/// callbacks here lets the current frame finish before a recording command pauses the
/// machine and captures its initial snapshot. Host operations that await work and
/// capture this synchronization context also resume on the emulation thread.
/// </remarks>
internal sealed class EmulationThread : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private readonly object _gate = new();
    private bool _stopping;
    private bool _acceptPosts = true;
    private int _pendingOperations;
    public EmulationThread()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "6502 Headless execution" };
        _thread.Start();
    }
    private void Run()
    {
        SetSynchronizationContext(this);
        foreach (var action in _queue.GetConsumingEnumerable()) action();
    }
    public override void Post(SendOrPostCallback callback, object? state)
    {
        lock (_gate)
        {
            // A disposed timer can still deliver its last callback during shutdown.
            if (_acceptPosts) _queue.Add(() => callback(state));
        }
    }
    public Task InvokeAsync(Action action) => InvokeAsync(() => { action(); return Task.CompletedTask; });
    public Task InvokeAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_stopping) return Task.FromException(new ObjectDisposedException(nameof(EmulationThread)));
            _pendingOperations++;
            _queue.Add(() => _ = CompleteActionAsync(action, completion));
        }
        return completion.Task;
    }

    private async Task CompleteActionAsync(Func<Task> action, TaskCompletionSource completion)
    {
        try { await action(); completion.TrySetResult(); }
        catch (Exception ex) { completion.TrySetException(ex); }
        finally
        {
            lock (_gate)
            {
                _pendingOperations--;
                CompleteQueueIfDrained();
            }
        }
    }

    private void CompleteQueueIfDrained()
    {
        if (_stopping && _pendingOperations == 0 && _acceptPosts)
        {
            _acceptPosts = false;
            _queue.CompleteAdding();
        }
    }
    public void Dispose()
    {
        if (Thread.CurrentThread == _thread)
            throw new InvalidOperationException("Close the emulation thread from its owner.");
        lock (_gate)
        {
            if (_stopping) return;
            _stopping = true;
            CompleteQueueIfDrained();
        }
        _thread.Join();
        _queue.Dispose();
    }
}
