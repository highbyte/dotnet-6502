using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Headless;

/// <summary>
/// Headless host app for running the emulator without any UI, rendering, audio, or user input.
/// Driven entirely by CLI parameters and Lua scripts.
/// </summary>
public class HeadlessHostApp : HostApp<NullInputHandlerContext, NullAudioHandlerContext>, IDebuggableHostApp, IRemotableHostApp
{
    private new readonly ILogger _logger;
    private readonly CancellationTokenSource _appCts;

    private HeadlessPeriodicTimer? _updateTimer;
    private HeadlessPeriodicTimer? _scriptingTickTimer;

    // IDebuggableHostApp
    public bool WaitForExternalDebugger { get; set; }

    private bool _isExternalDebuggerAttached;
    public bool IsExternalDebuggerAttached => _isExternalDebuggerAttached;

    private DebugAdapterLogic? _debugAdapter;
    private IExecEvaluator? _originalBreakpointEvaluator;

    public HeadlessHostApp(
        SystemList<NullInputHandlerContext, NullAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory,
        CancellationTokenSource appCts)
        : base("Headless", systemList, loggerFactory, useStatsNamePrefix: false)
    {
        _logger = loggerFactory.CreateLogger(typeof(HeadlessHostApp).Name);
        _appCts = appCts;

        var inputHandlerContext = new NullInputHandlerContext();
        var audioHandlerContext = new NullAudioHandlerContext();

        base.SetContexts(() => inputHandlerContext, () => audioHandlerContext);
        base.InitInputHandlerContext();
        base.InitAudioHandlerContext();

        // No call to SetRenderConfig() — HostApp.InitRendererForSystem() handles null gracefully.
    }

    // --- IDebuggableHostApp ---

    public void SetExternalDebugAdapter(DebugAdapterLogic debugAdapter)
    {
        _originalBreakpointEvaluator = CurrentSystemRunner?.CustomExecEvaluator;
        CurrentSystemRunner?.SetCustomExecEvaluator(debugAdapter.GetBreakpointEvaluator());
        _debugAdapter = debugAdapter;
        WaitForExternalDebugger = false;
        _isExternalDebuggerAttached = true;

        debugAdapter.OnDebuggerPaused = () => CurrentSystemRunner?.AudioHandler.PausePlaying();
        debugAdapter.OnDebuggerResumed = () => CurrentSystemRunner?.AudioHandler.StartPlaying();
    }

    public void ClearExternalDebugAdapter()
    {
        if (_debugAdapter != null)
        {
            _debugAdapter.OnDebuggerPaused = null;
            _debugAdapter.OnDebuggerResumed = null;
        }
        _debugAdapter = null;
        if (_originalBreakpointEvaluator != null)
            CurrentSystemRunner?.SetCustomExecEvaluator(_originalBreakpointEvaluator);
        else
            CurrentSystemRunner?.ClearCustomExecEvaluator();
        _isExternalDebuggerAttached = false;
    }

    // --- Lifecycle overrides ---

    public override void QuitApplication()
    {
        _logger.LogInformation("QuitApplication requested — cancelling app token.");
        _appCts.Cancel();
    }

    public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
    {
        StopAndDisposeUpdateTimer();
        _updateTimer = CreateUpdateTimerForSystem(CurrentSystemRunner!.System);
        _updateTimer.Start();
        base.OnAfterStart(emulatorStateBeforeStart);
    }

    public override void OnAfterPause()
    {
        base.OnAfterPause();
        _updateTimer?.Stop();
    }

    public override void OnAfterStop()
    {
        StopAndDisposeUpdateTimer();
        base.OnAfterStop();
    }

    public override void OnAfterClose()
    {
        base.OnAfterClose();
        StopAndDisposeUpdateTimer();
    }

    // --- Scripting timer ---

    protected override void OnScriptingEngineSet()
    {
        _scriptingTickTimer = CreateScriptingTickTimer();
        _scriptingTickTimer.Start();
        // Drain any pending actions synchronously on this thread
        DrainPendingScriptActionsAsync().GetAwaiter().GetResult();
    }

    protected override void StopScriptingTimer()
    {
        if (_scriptingTickTimer != null)
        {
            _scriptingTickTimer.Elapsed -= ScriptingTickTimerElapsed;
            _scriptingTickTimer.Stop();
            _scriptingTickTimer.Dispose();
            _scriptingTickTimer = null;
        }
    }

    // --- Frame execution gating ---

    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = true;
        shouldReceiveInput = false; // No user input in headless mode

        if (EmulatorState != EmulatorState.Running)
        {
            shouldRun = false;
            return;
        }

        if (WaitForExternalDebugger)
        {
            shouldRun = false;
            return;
        }

        if (IsExternalDebuggerAttached && _debugAdapter?.IsStopped == true)
        {
            shouldRun = false;
            return;
        }
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        // In headless mode, simply log breakpoint triggers
        if (execEvaluatorTriggerResult.Triggered)
        {
            _logger.LogInformation("Breakpoint triggered at PC=0x{PC:X4}", CurrentRunningSystem?.CPU.PC);
        }
    }

    // --- Timer helpers ---

    private HeadlessPeriodicTimer CreateUpdateTimerForSystem(ISystem system)
    {
        double updateIntervalMS = (1 / system.Screen.RefreshFrequencyHz) * 1000;
        var timer = new HeadlessPeriodicTimer { IntervalMilliseconds = updateIntervalMS };
        timer.Elapsed += UpdateTimerElapsed;
        return timer;
    }

    private void StopAndDisposeUpdateTimer()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Elapsed -= UpdateTimerElapsed;
            _updateTimer.Stop();
            _updateTimer.Dispose();
            _updateTimer = null;
        }
    }

    private HeadlessPeriodicTimer CreateScriptingTickTimer()
    {
        var timer = new HeadlessPeriodicTimer { IntervalMilliseconds = 16.0 }; // ~60 Hz
        timer.Elapsed += ScriptingTickTimerElapsed;
        return timer;
    }

    private async void ScriptingTickTimerElapsed(object? sender, EventArgs e)
    {
        try
        {
            InvokeScriptingTick();
            await DrainPendingScriptActionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in scripting tick timer.");
        }
    }

    private async void UpdateTimerElapsed(object? sender, EventArgs e)
    {
        try
        {
            RunEmulatorOneFrame();
            await DrainPendingScriptActionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in update timer.");
        }
    }

    // IRemotableHostApp — no rendering in headless
    public byte[]? CaptureScreenshotPng() => null;
}
