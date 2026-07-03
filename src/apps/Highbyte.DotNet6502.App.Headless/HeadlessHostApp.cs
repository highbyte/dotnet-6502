using System.Runtime.InteropServices;
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Timing;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Highbyte.DotNet6502.App.Headless;

/// <summary>
/// Headless host app for running the emulator without any UI, rendering, audio, or user input.
/// Driven entirely by CLI parameters and Lua scripts.
/// </summary>
public class HeadlessHostApp : HostApp, IDebuggableHostApp, IRemotableHostApp
{
    private new readonly ILogger _logger;
    private readonly CancellationTokenSource _appCts;

    private FrameTimer? _updateTimer;

    // IDebuggableHostApp
    public bool WaitForExternalDebugger { get; set; }

    private bool _isExternalDebuggerAttached;
    public bool IsExternalDebuggerAttached => _isExternalDebuggerAttached;

    private DebugAdapterLogic? _debugAdapter;
    private IExecEvaluator? _originalBreakpointEvaluator;

    public HeadlessHostApp(
        SystemList systemList,
        ILoggerFactory loggerFactory,
        CancellationTokenSource appCts)
        : base("Headless", systemList, loggerFactory, useStatsNamePrefix: false)
    {
        _logger = loggerFactory.CreateLogger(typeof(HeadlessHostApp).Name);
        _appCts = appCts;

        var inputHandlerContext = new NullInputHandlerContext();

        base.SetContexts(() => inputHandlerContext);
        base.InitInputHandlerContext();

        // No call to SetRenderConfig()/SetAudioConfig() — headless has no rendering or audio;
        // HostApp.InitRendererForSystem()/InitAudioForSystem() handle the absence gracefully.
    }

    // --- IDebuggableHostApp ---

    public void SetExternalDebugAdapter(DebugAdapterLogic debugAdapter)
    {
        _originalBreakpointEvaluator = CurrentSystemRunner?.CustomExecEvaluator;
        CurrentSystemRunner?.SetCustomExecEvaluator(debugAdapter.GetBreakpointEvaluator());
        _debugAdapter = debugAdapter;
        WaitForExternalDebugger = false;
        _isExternalDebuggerAttached = true;

        debugAdapter.OnDebuggerPaused = PauseAudio;
        debugAdapter.OnDebuggerResumed = ResumeAudio;
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

    protected override IScriptingTickTimer CreateScriptingTickTimer(double intervalMs) =>
        new FrameTimer { IntervalMilliseconds = intervalMs };

    protected override void OnScriptingEngineSet()
    {
        // Headless startup drains script actions explicitly from Program.cs.
    }

    internal async Task DrainStartupScriptActionsAsync()
    {
        await DrainPendingScriptActionsAsync().ConfigureAwait(false);
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

    private FrameTimer CreateUpdateTimerForSystem(ISystem system)
    {
        double updateIntervalMS = (1 / system.Screen.RefreshFrequencyHz) * 1000;
        var timer = new FrameTimer { IntervalMilliseconds = updateIntervalMS };
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

    private void UpdateTimerElapsed(object? sender, EventArgs e)
        => ObserveBackgroundTask(UpdateTimerElapsedAsync());

    private async Task UpdateTimerElapsedAsync()
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

    private void ObserveBackgroundTask(Task task)
    {
        if (task.IsCompleted)
        {
            LogBackgroundTaskFailure(task);
            return;
        }

        _ = task.ContinueWith(
            static (completedTask, state) => ((HeadlessHostApp)state!).LogBackgroundTaskFailure(completedTask),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void LogBackgroundTaskFailure(Task task)
    {
        if (task.Exception is { } exception)
        {
            _logger.LogError(exception.GetBaseException(), "Unhandled exception in update timer.");
        }
    }

    // IRemotableHostApp — screenshot capture.
    // Headless has no bitmap render target, so composite the current frame's layers directly
    // (same technique as LuaScreenshotProxy's emu.screenshot(), duplicated here to avoid pulling
    // an ImageSharp dependency into the shared Systems library).
    public byte[]? CaptureScreenshotPng()
    {
        var layerProvider = CurrentSystemRunner?.System.RenderProvider as IVideoFrameLayerProvider;
        if (layerProvider == null) return null;

        var layers = layerProvider.Layers;
        if (layers.Count == 0) return null;

        var size = layers[0].Size;
        int width = size.Width;
        int height = size.Height;

        var frontBuffers = layerProvider.CurrentFrontLayerBuffers;
        var byteSrcs = new List<ReadOnlyMemory<byte>>(frontBuffers.Count);
        foreach (var mem in frontBuffers)
        {
            var byteSpan = MemoryMarshal.AsBytes(mem.Span);
            byteSrcs.Add(byteSpan.ToArray());
        }

        int dstStride = width * 4;
        var dst = new byte[height * dstStride];
        SoftwareLayerCompositor.FlattenRgba32(dst, dstStride, size, layers, byteSrcs);

        // FlattenRgba32 output layout: [B, G, R, A] per pixel (BGRA32)
        using var image = Image.LoadPixelData<Bgra32>(dst, width, height);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
}
