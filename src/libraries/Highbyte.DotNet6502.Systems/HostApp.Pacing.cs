using System.Diagnostics;
using Highbyte.DotNet6502.Systems.Timing;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems;

public partial class HostApp
{
    private FramePacer? _framePacer;
    private EmulationSpeed _speed = EmulationSpeed.Normal;

    protected EmulationSpeed Speed
    {
        get => _speed;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_speed == value) return;
            _speed = value;
            if (_framePacer is { } previous)
                StartFramePacing(previous.Post, previous.YieldToNativeEvents);
        }
    }

    protected void StartFramePacing(Action<Action> post, bool yieldToNativeEvents = false)
    {
        StopFramePacing();
        _framePacer = new FramePacer(this, post, CurrentSystemRunner!.System.Screen.RefreshFrequencyHz, yieldToNativeEvents);
        _framePacer.Start();
    }

    protected void StopFramePacing()
    {
        _framePacer?.Dispose();
        _framePacer = null;
    }

    // The timer and I/O completions only post work. Frame execution, deadline changes and
    // Lua continuation state belong exclusively to the host dispatcher.
    private sealed class FramePacer : IDisposable
    {
        private readonly HostApp _host;
        private readonly Timer _timer;
        private readonly double _interval;
        private readonly int _idleDelayMs;
        private readonly Action _pump;
        private readonly Action _wake;
        private double _deadline;
        private double _timerDeadline;
        private volatile bool _stopped;
        public Action<Action> Post { get; }
        public bool YieldToNativeEvents { get; }

        public FramePacer(HostApp host, Action<Action> post, double nativeHz, bool yieldToNativeEvents)
        {
            _host = host;
            Post = post;
            YieldToNativeEvents = yieldToNativeEvents;
            _pump = Pump;
            _wake = () => Post(_pump);
            _timer = new Timer(OperatingSystem.IsBrowser() ? _ => _wake() : WakeAtDeadline);
            _interval = host.Speed.Multiplier is double multiplier
                ? Stopwatch.Frequency / nativeHz / multiplier : 0;
            _idleDelayMs = Math.Max(1, (int)Math.Ceiling(1000 / nativeHz));
        }

        public void Start()
        {
            _deadline = Stopwatch.GetTimestamp() + _interval;
            _wake();
        }

        private void WakeAtDeadline(object? state)
        {
            // OS timers can coalesce by several milliseconds. Finish the short tail on
            // the timer thread so the guest stays in phase without blocking the UI.
            double remaining;
            while (!_stopped && (remaining = MillisecondsUntil(Volatile.Read(ref _timerDeadline))) is > 0 and <= 5)
            {
                if (remaining > 1.5) Thread.Sleep(1);
                else Thread.SpinWait(32);
            }
            if (!_stopped) _wake();
        }

        private static double MillisecondsUntil(double deadline) =>
            (deadline - Stopwatch.GetTimestamp()) * 1000 / Stopwatch.Frequency;

        private async void Pump()
        {
            try
            {
                var batchStart = Stopwatch.GetTimestamp();
                while (!_stopped)
                {
                    if (!ReadyToRun()) return;
                    var now = Stopwatch.GetTimestamp();
                    bool ran = _host.TryRunEmulatorOneFrame();
                    await _host.DrainPendingScriptActionsAsync();
                    if (_stopped) return;
                    if (ran)
                    {
                        // Keep phase through short stalls (e.g. a GC during a Lua hook).
                        // Drop the backlog after a long stall. Holds rebase separately on release.
                        _deadline += _interval;
                        if (now - _deadline > 5 * _interval)
                            _deadline = now + _interval;
                    }
                    if (_host._scriptingEngine.FrameHold is { Finished: false } hold)
                    {
                        WaitForHold(hold);
                        return;
                    }
                    if (!ran)
                    {
                        // The monitor or external debugger owns execution.
                        _timer.Change(_idleDelayMs, Timeout.Infinite);
                        return;
                    }

                    if (Stopwatch.GetElapsedTime(batchStart).TotalMilliseconds >= 4)
                    {
                        // A continuously nonempty Avalonia queue can keep the macOS run loop
                        // inside the dispatcher, even at Background priority.
                        if (YieldToNativeEvents) _timer.Change(1, Timeout.Infinite);
                        else _wake();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _host._logger.LogError(ex, "Emulation frame failed.");
                if (!_stopped) _host.Pause();
            }
        }

        private bool ReadyToRun()
        {
            if (_host._scriptingEngine.FrameHold is { Finished: false } held && !ResumeHold(held))
                return false;

            var remainingMs = MillisecondsUntil(_deadline);
            if (remainingMs <= 0) return true;
            // WASM timers share the UI thread. Wake on a later browser turn at
            // the deadline. The native timer can wake early and finish on its own thread.
            double delayMs = Math.Ceiling(remainingMs);
            if (!OperatingSystem.IsBrowser())
            {
                Volatile.Write(ref _timerDeadline, _deadline);
                delayMs = remainingMs - 3;
            }
            _timer.Change((int)Math.Clamp(delayMs, 0, int.MaxValue), Timeout.Infinite);
            return false;
        }

        private bool ResumeHold(ScriptFrameHold hold)
        {
            _host.OnBeforeRunEmulatorOneFrame(out bool shouldRun, out _);
            if (!shouldRun)
            {
                _timer.Change(_idleDelayMs, Timeout.Infinite);
                return false;
            }
            if (hold.PendingIO is { IsCompleted: false } || !_host._scriptingEngine.PollFrameHold())
            {
                // A resumed hook can immediately yield again.
                if (!_stopped) WaitForHold(hold);
                return false;
            }
            var released = Stopwatch.GetTimestamp();
            if (released - _deadline >= _interval)
                _deadline = released;
            return true;
        }

        private void WaitForHold(ScriptFrameHold hold)
        {
            if (hold.PendingIO is { } io)
                io.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(_wake);
            else
                _timer.Change(_idleDelayMs, Timeout.Infinite); // emu.yield() waits for a tick
        }

        public void Dispose()
        {
            _stopped = true;
            _timer.Dispose();
        }
    }
}
