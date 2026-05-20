using System.Data;
using System.Text;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.AspNet.Audio;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Timing;
using Toolbelt.Blazor.Gamepad;

namespace Highbyte.DotNet6502.App.WASM.Emulator.Skia;

public class SkiaWASMHostApp : HostApp
{
    // --------------------
    // Injected variables
    // --------------------
    private new readonly ILogger _logger;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly Func<SKCanvas> _getCanvas;
    private readonly Func<GRContext> _getGrContext;
    private readonly Func<AudioContextSync> _getAudioContext;
    private readonly GamepadList _gamepadList;

    public EmulatorConfig EmulatorConfig => _emulatorConfig;

    private readonly float _defaultAudioVolumePercent;
    private readonly ILoggerFactory _loggerFactory;

    // --------------------
    // Other variables / constants
    // --------------------
    private AspNetInputHandlerContext _inputHandlerContext = default!;
    private WASMAudioHandlerContext _audioHandlerContext = default!;

    private readonly IJSRuntime _jsRuntime;
    private readonly IWasmHostView _wasmHostUIViewModel;
    private FrameTimer? _updateTimer;

    private WasmMonitor _monitor = default!;
    public WasmMonitor Monitor => _monitor;

    private const int STATS_EVERY_X_FRAME = 60 * 1;
    private int _statsFrameCount = 0;

    private const int DEBUGMESSAGE_EVERY_X_FRAME = 1;
    private int _debugFrameCount = 0;

    private AspNetRenderLoop? _renderloop;

    /// <summary>
    /// Engine plug-ins that contribute system-specific render targets. Invoked from
    /// <see cref="InitTargetRenderers"/> so no system-specific render code lives here.
    /// </summary>
    private readonly IEnumerable<ISkiaWasmRenderTargetPlugin> _renderTargetPlugins;

    public SkiaWASMHostApp(
        SystemList systemList,
        ILoggerFactory loggerFactory,
        EmulatorConfig emulatorConfig,

        Func<SKCanvas> getCanvas,
        Func<GRContext> getGrContext,
        Func<AudioContextSync> getAudioContext,
        GamepadList gamepadList,
        IJSRuntime jsRuntime,
        IWasmHostView wasmHostUIViewModel,
        IEnumerable<ISkiaWasmRenderTargetPlugin> renderTargetPlugins
        ) : base("SilkNet", systemList, loggerFactory)
    {
        _renderTargetPlugins = renderTargetPlugins;
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

        _defaultAudioVolumePercent = 20.0f;

        _inputHandlerContext = new AspNetInputHandlerContext(
            _loggerFactory, _gamepadList, DetectIsRunningOnMacOS(), DetectKeyboardLayoutId());
        _audioHandlerContext = new WASMAudioHandlerContext(_getAudioContext, _jsRuntime, _defaultAudioVolumePercent);

        InitTargetRenderers(); // New rendering pipeline

        base.SetContexts(() => _inputHandlerContext);

        // Audio pipeline configuration: register the WebAudio host audio target.
        base.SetAudioConfig(atp =>
            atp.AddAudioTargetType<WebAudioCommandTarget>(
                () => new WebAudioCommandTarget(_audioHandlerContext, _loggerFactory)));
    }

    /// <summary>
    /// Initializes the WebAudio host context. Must be called after a user gesture in the browser.
    /// </summary>
    public void InitAudioContext() => _audioHandlerContext.Init();

    // Detects whether the browser runs on macOS. The .NET WASM runtime reports OSPlatform.Browser
    // (not the underlying OS), so the macOS ISO-keyboard §/< keycode swap correction needs this
    // browser-side check. window.getNavigatorPlatform is defined in wwwroot/index.html.
    private bool DetectIsRunningOnMacOS()
    {
        try
        {
            if (_jsRuntime is IJSInProcessRuntime jsInProcess)
            {
                var platform = jsInProcess.Invoke<string>("getNavigatorPlatform") ?? string.Empty;
                bool isMacOS = platform.Contains("Mac", StringComparison.OrdinalIgnoreCase);
                _logger.LogInformation($"Browser navigator platform: '{platform}' (macOS: {isMacOS})");
                return isMacOS;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not detect browser OS platform; assuming not macOS.");
        }
        return false;
    }

    // Detects the browser's keyboard layout, for auto-selecting the emulated keyboard layout when
    // the user has not pinned one. Uses the Keyboard Map API (Chromium only — Safari/Firefox lack
    // it); window.getKeyboardLayoutId is defined in wwwroot/index.html, where the async detection
    // runs once at page load and caches a result for this synchronous getter. Returns null when
    // unsupported or not yet detected, so resolution falls back to OS culture.
    private string? DetectKeyboardLayoutId()
    {
        try
        {
            if (_jsRuntime is IJSInProcessRuntime jsInProcess)
            {
                var layoutId = jsInProcess.Invoke<string>("getKeyboardLayoutId") ?? string.Empty;
                _logger.LogInformation($"Browser detected keyboard layout id: '{layoutId}'");
                return string.IsNullOrEmpty(layoutId) ? null : layoutId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not detect browser keyboard layout.");
        }
        return null;
    }

    private void InitTargetRenderers()
    {
        base.SetRenderConfig(
            (RenderTargetProvider rtp) =>
            {
                // System-agnostic render targets — available regardless of the emulated system.
                rtp.AddRenderTargetType<SkiaCanvasTwoLayerRenderTarget>(() => new SkiaCanvasTwoLayerRenderTarget(
                    new RenderSize(CurrentRunningSystem!.Screen.VisibleWidth, CurrentRunningSystem!.Screen.VisibleHeight),
                    _getCanvas,
                    flush: false));

                // Experimental Skia command based target. WIP.
                rtp.AddRenderTargetType<SkiaCommandTarget>(() => new SkiaCommandTarget(
                    _getCanvas,
                    useCellCoordinates: true,
                    flush: false));

                // System-specific render targets come from engine plug-ins (Impl.AspNet.<System>).
                var renderContext = new SkiaWasmRenderContext(
                    _getCanvas,
                    () => CurrentRunningSystem!);
                foreach (var renderTargetPlugin in _renderTargetPlugins)
                    renderTargetPlugin.RegisterRenderTargets(rtp, renderContext);
            },
            () =>
            {
                var renderloop = new AspNetRenderLoop(
                    OnBeforeRender,
                    OnAfterRender,
                    shouldEmitEmulationFrame: () => EmulatorState != EmulatorState.Uninitialized);
                _renderloop = renderloop;
                return renderloop;
            });
    }

    /// <summary>
    /// Callback from AspNetRenderLoop (new rendering pipleline) before the emulator has been rendered to the screen.
    /// </summary>
    public void OnBeforeRender(double? deltaTime)
    {
        var emulatorWillBeRendered = EmulatorState == EmulatorState.Running;
        if (emulatorWillBeRendered)
        {
            // TODO: Shouldn't scale be able to set once we start the emulator (OnBeforeStart method?) instead of every frame?
            _getCanvas().Scale((float)_emulatorConfig.CurrentDrawScale);
        }
    }

    /// <summary>
    /// Callback from AspNetRenderLoop (new rendering pipleline) after the emulator has been rendered to the screen.
    /// </summary>
    public void OnAfterRender(double? deltaTime)
    {
    }


    public override void OnAfterSelectedSystemChanged()
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

    public override void OnBeforeStop()
    {
    }

    public override void OnAfterStop()
    {
        WasmTaskHelper.Observe(_wasmHostUIViewModel.SetDebugState(visible: false), "SetDebugState(false)");
        WasmTaskHelper.Observe(_wasmHostUIViewModel.SetStatsState(visible: false), "SetStatsState(false)");
        _monitor.Disable();

        _updateTimer!.Stop();
        _updateTimer!.Dispose();
        _updateTimer = null;
    }

    public override void OnAfterClose()
    {
        // Cleanup contexts
        _inputHandlerContext?.Cleanup();
        _audioHandlerContext?.Cleanup();
    }


    private FrameTimer CreateUpdateTimerForSystem(ISystem system)
    {
        // Number of milliseconds between each invokation of the main loop. 60 fps -> (1/60) * 1000  -> approx 16.6667ms
        double updateIntervalMS = (1 / system.Screen.RefreshFrequencyHz) * 1000;
        var updateTimer = new FrameTimer();
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


    // New rendering pipeline with AspNetRenderLoop
    public void RaiseRenderLoopTick()
    {
        GetRenderLoopOrThrow().RaiseFrameTick();
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
        var inputDebugInfo = CurrentSystemRunner!.System.InputConsumer?.GetDebugInfo() ?? new List<string>();
        var inputStatsOneString = string.Join(" # ", inputDebugInfo);
        sb.Append($"{BuildHtmlString("INPUT", "header")}: {BuildHtmlString(inputStatsOneString, "value")} ");
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
            WasmTaskHelper.Observe(_wasmHostUIViewModel.ToggleDebugState(), nameof(_wasmHostUIViewModel.ToggleDebugState));
            WasmTaskHelper.Observe(_wasmHostUIViewModel.ToggleStatsState(), nameof(_wasmHostUIViewModel.ToggleStatsState));
        }
        else if (key == "F12" && (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused))
        {
            ToggleMonitor();
        }
        else if (key == "F9" && EmulatorState == EmulatorState.Running)
        {
            var currentSystemRunner = CurrentSystemRunner ?? throw new DotNet6502Exception("No current system runner is active.");
            var inputConsumer = currentSystemRunner.System.InputConsumer;
            var codingAssistantEnabledProp = inputConsumer?.GetType().GetProperty("CodingAssistantEnabled");
            if (codingAssistantEnabledProp?.PropertyType == typeof(bool) && codingAssistantEnabledProp.CanRead && codingAssistantEnabledProp.CanWrite)
            {
                var toggledAssistantState = !((bool?)codingAssistantEnabledProp.GetValue(inputConsumer) ?? false);
                codingAssistantEnabledProp.SetValue(inputConsumer, toggledAssistantState);

                var defaultEnabledProp = CurrentHostSystemConfig.GetType().GetProperty("BasicAIAssistantDefaultEnabled");
                if (defaultEnabledProp?.PropertyType == typeof(bool) && defaultEnabledProp.CanWrite)
                    defaultEnabledProp.SetValue(CurrentHostSystemConfig, toggledAssistantState);
            }
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

    private AspNetRenderLoop GetRenderLoopOrThrow()
    {
        return _renderloop ?? throw new DotNet6502Exception("Render loop has not been initialized.");
    }
}
