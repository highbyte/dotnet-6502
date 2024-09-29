using System.Data;
using System.Text;
using Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Toolbelt.Blazor.Gamepad;

namespace Highbyte.DotNet6502.App.WASM.Emulator.Skia;

public class SkiaWASMHostApp : HostApp<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext>
{
    // --------------------
    // Injected variables
    // --------------------
    private readonly ILogger _logger;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly Func<SKCanvas> _getCanvas;
    private readonly Func<GRContext> _getGrContext;
    private readonly Func<AudioContextSync> _getAudioContext;
    private readonly GamepadList _gamepadList;

    public EmulatorConfig EmulatorConfig => _emulatorConfig;

    private readonly bool _defaultAudioEnabled;
    private readonly float _defaultAudioVolumePercent;
    private readonly ILoggerFactory _loggerFactory;

    // --------------------
    // Other variables / constants
    // --------------------
    private SkiaRenderContext _renderContext = default!;
    private AspNetInputHandlerContext _inputHandlerContext = default!;
    private WASMAudioHandlerContext _audioHandlerContext = default!;

    private readonly IJSRuntime _jsRuntime;
    private readonly Highbyte.DotNet6502.App.WASM.Pages.Index _wasmHostUIViewModel;
    private PeriodicAsyncTimer? _updateTimer;

    private WasmMonitor _monitor = default!;
    public WasmMonitor Monitor => _monitor;

    private const int STATS_EVERY_X_FRAME = 60 * 1;
    private int _statsFrameCount = 0;

    private const int DEBUGMESSAGE_EVERY_X_FRAME = 1;
    private int _debugFrameCount = 0;

    public SkiaWASMHostApp(
        SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory,
        EmulatorConfig emulatorConfig,

        Func<SKCanvas> getCanvas,
        Func<GRContext> getGrContext,
        Func<AudioContextSync> getAudioContext,
        GamepadList gamepadList,
        IJSRuntime jsRuntime,
        Highbyte.DotNet6502.App.WASM.Pages.Index wasmHostUIViewModel
        ) : base("SilkNet", systemList, loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(SkiaWASMHostApp).Name);
        _emulatorConfig = emulatorConfig;
        _emulatorConfig.CurrentDrawScale = _emulatorConfig.DefaultDrawScale;

        _getCanvas = getCanvas;
        _getGrContext = getGrContext;
        _getAudioContext = getAudioContext;
        _gamepadList = gamepadList;
        _jsRuntime = jsRuntime;
        _wasmHostUIViewModel = wasmHostUIViewModel;

        _defaultAudioEnabled = false;
        _defaultAudioVolumePercent = 20.0f;

        _renderContext = new SkiaRenderContext(_getCanvas, _getGrContext);
        _inputHandlerContext = new AspNetInputHandlerContext(_loggerFactory, _gamepadList);
        _audioHandlerContext = new WASMAudioHandlerContext(_getAudioContext, _jsRuntime, _defaultAudioVolumePercent);

        base.SetContexts(getRenderContext: () => _renderContext, getInputHandlerContext: () => _inputHandlerContext, getAudioHandlerContext: () => _audioHandlerContext);
    }

    public override void OnAfterSelectSystem()
    {
    }

    public override bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        // Force a full GC to free up memory, so it won't risk accumulate memory usage if GC has not run for a while.
        var m0 = GC.GetTotalMemory(forceFullCollection: true);
        _logger.LogInformation("Allocated memory before starting emulator: " + m0);

        return true;
    }

    public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
    {
        // Create timer for current system on initial start. Assume Stop() sets _updateTimer to null.
        if (_updateTimer == null)
        {
            _updateTimer = CreateUpdateTimerForSystem(CurrentSystemRunner!.System);
        }
        _updateTimer!.Start();

        // Init monitor for current system started if this system was not started before
        if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
            _monitor = new WasmMonitor(_jsRuntime, CurrentSystemRunner!, _emulatorConfig, _wasmHostUIViewModel);
    }

    public override void OnAfterPause()
    {
        _updateTimer!.Stop();
    }

    public override void OnAfterStop()
    {
        _wasmHostUIViewModel.SetDebugState(visible: false);
        _wasmHostUIViewModel.SetStatsState(visible: false);
        _monitor.Disable();

        _updateTimer!.Stop();
        _updateTimer!.Dispose();
        _updateTimer = null;
    }

    public override void OnAfterClose()
    {
        // Cleanup contexts
        _renderContext?.Cleanup();
        _inputHandlerContext?.Cleanup();
        _audioHandlerContext?.Cleanup();
    }


    private PeriodicAsyncTimer CreateUpdateTimerForSystem(ISystem system)
    {
        // Number of milliseconds between each invokation of the main loop. 60 fps -> (1/60) * 1000  -> approx 16.6667ms
        double updateIntervalMS = (1 / system.Screen.RefreshFrequencyHz) * 1000;
        var updateTimer = new PeriodicAsyncTimer();
        updateTimer.IntervalMilliseconds = updateIntervalMS;
        updateTimer.Elapsed += UpdateTimerElapsed;
        return updateTimer;
    }

    private void UpdateTimerElapsed(object? sender, EventArgs e) => RunEmulatorOneFrame();

    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = false;
        shouldReceiveInput = false;
        // Don't update emulator state when monitor is visible
        if (_monitor.Visible)
            return;

        shouldRun = true;
        shouldReceiveInput = true;
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        // Push debug info to debug UI
        _debugFrameCount++;
        if (_debugFrameCount >= DEBUGMESSAGE_EVERY_X_FRAME)
        {
            _debugFrameCount = 0;
            var sb = new StringBuilder();

            GetIODebugMessagesHtmlString(sb);
            if (sb.Length > 0)
                sb.Append("<br />");
            GetSystemDebugMessagesHtmlString(sb);

            _wasmHostUIViewModel.UpdateDebug(sb.ToString());
        }

        // Push stats to stats UI
        if (CurrentRunningSystem!.InstrumentationEnabled)
        {
            _statsFrameCount++;
            if (_statsFrameCount >= STATS_EVERY_X_FRAME)
            {
                _statsFrameCount = 0;
                _wasmHostUIViewModel.UpdateStats(GetStatsHtmlString());
            }
        }

        // Show monitor if we encounter breakpoint or other break
        if (execEvaluatorTriggerResult.Triggered)
            _monitor.Enable(execEvaluatorTriggerResult);
    }

    /// <summary>
    /// Called from ASP.NET Blazor SKGLView "OnPaintSurface" event to render one frame.
    /// </summary>
    /// <param name="args"></param>
    public void Render()
    {
        // Draw emulator on screen
        base.DrawFrame();
    }

    public override void OnBeforeDrawFrame(bool emulatorWillBeRendered)
    {
        if (emulatorWillBeRendered)
        {
            // TODO: Shouldn't scale be able to set once we start the emulator (OnBeforeStart method?) instead of every frame?
            _getCanvas().Scale((float)_emulatorConfig.CurrentDrawScale);
        }
    }

    public override void OnAfterDrawFrame(bool emulatorRendered)
    {
        if (emulatorRendered)
        {
        }
    }

    public void SetVolumePercent(float volumePercent)
    {
        _audioHandlerContext.SetMasterVolumePercent(masterVolumePercent: volumePercent);
    }

    private string GetStatsHtmlString()
    {
        string stats = "";

        var allStats = GetStats();
        foreach ((string name, IStat stat) in allStats.OrderBy(i => i.name))
        {
            if (stat.ShouldShow())
            {
                if (stats != "")
                    stats += "<br />";
                stats += $"{BuildHtmlString(name, "header")}: {BuildHtmlString(stat.GetDescription(), "value")} ";
            }
        }
        return stats;
    }

    private void GetIODebugMessagesHtmlString(StringBuilder sb)
    {
        var inputDebugInfo = CurrentSystemRunner!.InputHandler.GetDebugInfo();
        var inputStatsOneString = string.Join(" # ", inputDebugInfo);
        sb.Append($"{BuildHtmlString("INPUT", "header")}: {BuildHtmlString(inputStatsOneString, "value")} ");
        //foreach (var message in inputDebugInfo)
        //{
        //    if (debugMessages != "")
        //        debugMessages += "<br />";
        //    debugMessages += $"{BuildHtmlString("DEBUG INPUT", "header")}: {BuildHtmlString(message, "value")} ";
        //}

        var audioDebugInfo = CurrentSystemRunner!.AudioHandler.GetDebugInfo();
        foreach (var message in audioDebugInfo)
        {
            if (sb.Length > 0)
                sb.Append("<br />");
            sb.Append($"{BuildHtmlString("AUDIO", "header")}: {BuildHtmlString(message, "value")} ");
        }
    }

    private void GetSystemDebugMessagesHtmlString(StringBuilder sb)
    {
        var systemDebugInfo = CurrentSystemRunner!.System.DebugInfo;
        foreach (var systemDebugInfoItem in systemDebugInfo)
        {
            if (sb.Length > 0)
                sb.Append("<br />");
            sb.Append($"{BuildHtmlString(systemDebugInfoItem.Key, "header")}: {BuildHtmlString(systemDebugInfoItem.Value(), "value")} ");
        }
    }

    private string BuildHtmlString(string message, string cssClass, bool startNewLine = false)
    {
        string html = "";
        if (startNewLine)
            html += "<br />";
        html += $@"<span class=""{cssClass}"">{HttpUtility.HtmlEncode(message)}</span>";
        return html;
    }

    /// <summary>
    /// Receive Key Down event in emulator canvas.
    /// Also check for special non-emulator functions such as monitor and stats/debug
    /// </summary>
    /// <param name="e"></param>
    public void OnKeyDown(KeyboardEventArgs e)
    {
        // Send event to emulator
        _inputHandlerContext.KeyDown(e);

        // Check for other emulator functions
        var key = e.Key;
        if (key == "F11")
        {
            _wasmHostUIViewModel.ToggleDebugState();
            _wasmHostUIViewModel.ToggleStatsState();
        }
        else if (key == "F12" && (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused))
        {
            ToggleMonitor();
        }
        else if (key == "F9" && EmulatorState == EmulatorState.Running)
        {
            var toggeledAssistantState = !((C64AspNetInputHandler)CurrentSystemRunner.InputHandler).CodingAssistantEnabled;
            ((C64AspNetInputHandler)CurrentSystemRunner.InputHandler).CodingAssistantEnabled = toggeledAssistantState;
            ((C64HostConfig)CurrentHostSystemConfig).BasicAIAssistantDefaultEnabled = toggeledAssistantState;
        }
    }

    /// <summary>
    /// Receive Key Up event in emulator canvas.
    /// Also check for special non-emulator functions such as monitor and stats/debug
    /// </summary>
    /// <param name="e"></param>
    public void OnKeyUp(KeyboardEventArgs e)
    {
        // Send event to emulator
        _inputHandlerContext.KeyUp(e);
    }

    /// <summary>
    /// Receive Focus on emulator canvas.
    /// </summary>
    /// <param name="e"></param>
    public void OnFocus(FocusEventArgs e)
    {
        _inputHandlerContext.OnFocus(e);
    }

    public void ToggleMonitor()
    {
        if (Monitor.Visible)
        {
            Monitor.Disable();
        }
        else
        {
            Monitor.Enable();
        }
    }
}
