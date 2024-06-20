using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems;
using Toolbelt.Blazor.Gamepad;

namespace Highbyte.DotNet6502.App.WASM.Skia;

public class WasmHost : IDisposable
{
    public bool Initialized { get; private set; }

    private readonly IJSRuntime _jsRuntime;

    private PeriodicAsyncTimer? _updateTimer;

    private SystemRunner _systemRunner = default!;
    public SystemRunner SystemRunner => _systemRunner;

    private SKCanvas _skCanvas = default!;
    private GRContext _grContext = default!;

    private SkiaRenderContext _skiaRenderContext = default!;
    public WASMAudioHandlerContext AudioHandlerContext { get; private set; } = default!;
    public AspNetInputHandlerContext InputHandlerContext { get; private set; } = default!;

    private readonly string _systemName;
    private readonly SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext> _systemList;
    public SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext> SystemList => _systemList;
    private readonly Action<string> _updateStats;
    private readonly Action<string> _updateDebug;
    private readonly Func<bool, Task> _setMonitorState;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly Func<Task> _toggleDebugStatsState;
    private readonly ILoggerFactory _loggerFactory;
    private readonly float _initialMasterVolume;
    private readonly ILogger _logger;

    public WasmMonitor Monitor { get; private set; } = default!;


    private readonly Instrumentations _systemInstrumentations = new();
    private const string HostStatRootName = "WASM";
    private const string SystemTimeStatName = "Emulator-SystemTime";
    private const string RenderTimeStatName = "RenderTime";
    private const string InputTimeStatName = "InputTime";
    private const string AudioTimeStatName = "AudioTime";
    private ElapsedMillisecondsTimedStatSystem _systemTime;
    private ElapsedMillisecondsTimedStatSystem _renderTime;
    private ElapsedMillisecondsTimedStatSystem _inputTime;
    private readonly PerSecondTimedStat _updateFps;
    private readonly PerSecondTimedStat _renderFps;

    private const int STATS_EVERY_X_FRAME = 60 * 1;
    private int _statsFrameCount = 0;

    private const int DEBUGMESSAGE_EVERY_X_FRAME = 1;
    private int _debugFrameCount = 0;

    public WasmHost(
        IJSRuntime jsRuntime,
        string systemName,
        SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext> systemList,
        Action<string> updateStats,
        Action<string> updateDebug,
        Func<bool, Task> setMonitorState,
         EmulatorConfig emulatorConfig,
        Func<Task> toggleDebugStatsState,
        ILoggerFactory loggerFactory,
        float scale = 1.0f,
        float initialMasterVolume = 50.0f)
    {
        _jsRuntime = jsRuntime;
        _systemName = systemName;
        _systemList = systemList;
        _updateStats = updateStats;
        _updateDebug = updateDebug;
        _setMonitorState = setMonitorState;
        _emulatorConfig = emulatorConfig;
        _toggleDebugStatsState = toggleDebugStatsState;
        _initialMasterVolume = initialMasterVolume;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(WasmHost).Name);

        // Init stats
        InstrumentationBag.Clear();
        _updateFps = InstrumentationBag.Add<PerSecondTimedStat>($"{HostStatRootName}-OnUpdateFPS");
        _renderFps = InstrumentationBag.Add<PerSecondTimedStat>($"{HostStatRootName}-OnRenderFPS");

        Initialized = false;
    }

    public async Task Init(SKCanvas canvas, GRContext grContext, AudioContextSync audioContext, GamepadList gamepadList, IJSRuntime jsRuntime)
    {
        _skCanvas = canvas;
        _grContext = grContext;

        _skiaRenderContext = new SkiaRenderContext(GetCanvas, GetGRContext);
        InputHandlerContext = new AspNetInputHandlerContext(_loggerFactory, gamepadList);
        AudioHandlerContext = new WASMAudioHandlerContext(audioContext, jsRuntime, _initialMasterVolume);

        _systemList.InitContext(() => _skiaRenderContext, () => InputHandlerContext, () => AudioHandlerContext);

        _systemRunner = await _systemList.BuildSystemRunner(_systemName);

        Monitor = new WasmMonitor(_jsRuntime, _systemRunner, _emulatorConfig, _setMonitorState);

        // Init instrumentation
        _systemInstrumentations.Clear();
        _systemTime = _systemInstrumentations.Add($"{HostStatRootName}-{SystemTimeStatName}", new ElapsedMillisecondsTimedStatSystem(_systemRunner.System));
        _inputTime = _systemInstrumentations.Add($"{HostStatRootName}-{InputTimeStatName}", new ElapsedMillisecondsTimedStatSystem(_systemRunner.System));
        //_audioTime = _systemInstrumentations.Add($"{HostStatRootName}-{AudioTimeStatName}", new ElapsedMillisecondsTimedStatSystem(_systemRunner.System));
        _renderTime = _systemInstrumentations.Add($"{HostStatRootName}-{RenderTimeStatName}", new ElapsedMillisecondsTimedStatSystem(_systemRunner.System));

        Initialized = true;
    }

    public void Stop()
    {
        _updateTimer?.Stop();

        _systemRunner.AudioHandler.PausePlaying();

        _logger.LogInformation($"System stopped: {_systemName}");
    }

    public void Start()
    {
        if (_systemRunner != null && _systemRunner.AudioHandler != null)
            _systemRunner.AudioHandler.StartPlaying();

        if (_updateTimer != null)
        {
        }
        else
        {
            var screen = _systemList.GetSystem(_systemName).Result.Screen;
            // Number of milliseconds between each invokation of the main loop. 60 fps -> (1/60) * 1000  -> approx 16.6667ms
            double updateIntervalMS = (1 / screen.RefreshFrequencyHz) * 1000;
            _updateTimer = new PeriodicAsyncTimer();
            _updateTimer.IntervalMilliseconds = updateIntervalMS;
            _updateTimer.Elapsed += UpdateTimerElapsed;
        }
        _updateTimer!.Start();

        _logger.LogInformation($"System started: {_systemName}");
    }

    public void Cleanup()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer = null;
        }

        // Clear canvas
        _skiaRenderContext.GetCanvas().Clear();

        // Clean up Skia resources
        _skiaRenderContext?.Cleanup();

        // Clean up input handler resources
        InputHandlerContext?.Cleanup();

        // Stop any playing audio
        _systemRunner.AudioHandler.StopPlaying();
        // Clean up audio resources
        //AudioHandlerContext?.Cleanup();
    }

    private void UpdateTimerElapsed(object? sender, EventArgs e) => EmulatorRunOneFrame();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    private void EmulatorRunOneFrame()
    {

        if (!Initialized)
            return;

        if (Monitor.Visible)
            return;

        _updateFps.Update();

        _debugFrameCount++;
        if (_debugFrameCount >= DEBUGMESSAGE_EVERY_X_FRAME)
        {
            _debugFrameCount = 0;
            var debugString = GetDebugMessages();
            _updateDebug(debugString);
        }

        //_emulatorHelper.GenerateRandomNumber();
        _inputTime.Start();
        _systemRunner.ProcessInput();
        _inputTime.Stop();

        ExecEvaluatorTriggerResult execEvaluatorTriggerResult;
        _systemTime.Start();
        execEvaluatorTriggerResult = _systemRunner.RunEmulatorOneFrame();
        _systemTime.Stop();

        if (_systemRunner.System.InstrumentationEnabled)
        {
            _statsFrameCount++;
            if (_statsFrameCount >= STATS_EVERY_X_FRAME)
            {
                _statsFrameCount = 0;
                var statsString = GetStats();
                _updateStats(statsString);
            }
        }

        // Show monitor if we encounter breakpoint or other break
        if (execEvaluatorTriggerResult.Triggered)
        {
            Monitor.Enable(execEvaluatorTriggerResult);
        }
    }

    public void Render(SKCanvas canvas, GRContext grContext)
    {
        //if (Monitor.Visible)
        //    return;

        _renderFps.Update();

        _grContext = grContext;
        _skCanvas = canvas;
        _skCanvas.Scale((float)_emulatorConfig.CurrentDrawScale);

        _renderTime.Start();
        _systemRunner.Draw();
        //using (new SKAutoCanvasRestore(skCanvas))
        //{
        //    _systemRunner.Draw(skCanvas);
        //}
        _renderTime.Stop();
    }

    private SKCanvas GetCanvas()
    {
        return _skCanvas;
    }

    private GRContext GetGRContext()
    {
        return _grContext;
    }

    private string GetStats()
    {
        string stats = "";

        var allStats = InstrumentationBag.Stats
            .Union(_systemInstrumentations.Stats)
            .Union(_systemRunner.System.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{SystemTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.Renderer.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{RenderTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.AudioHandler.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{AudioTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.InputHandler.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{InputTimeStatName}-{x.Name}", x.Stat)))
            .ToList();
        foreach ((string name, IStat stat) in allStats.OrderBy(i => i.Name))
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

    private string GetDebugMessages()
    {
        string debugMessages = "";

        var inputDebugInfo = _systemRunner.InputHandler.GetDebugInfo();
        var inputStatsOneString = string.Join(" # ", inputDebugInfo);
        debugMessages += $"{BuildHtmlString("INPUT", "header")}: {BuildHtmlString(inputStatsOneString, "value")} ";
        //foreach (var message in inputDebugInfo)
        //{
        //    if (debugMessages != "")
        //        debugMessages += "<br />";
        //    debugMessages += $"{BuildHtmlString("DEBUG INPUT", "header")}: {BuildHtmlString(message, "value")} ";
        //}

        var audioDebugInfo = _systemRunner.AudioHandler.GetDebugInfo();
        foreach (var message in audioDebugInfo)
        {
            if (debugMessages != "")
                debugMessages += "<br />";
            debugMessages += $"{BuildHtmlString("AUDIO", "header")}: {BuildHtmlString(message, "value")} ";
        }

        return debugMessages;
    }

    private string BuildHtmlString(string message, string cssClass, bool startNewLine = false)
    {
        string html = "";
        if (startNewLine)
            html += "<br />";
        html += $@"<span class=""{cssClass}"">{HttpUtility.HtmlEncode(message)}</span>";
        return html;
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// Enable / Disable emulator functions such as monitor and stats/debug
    /// </summary>
    /// <param name="e"></param>
    public void OnKeyDown(KeyboardEventArgs e)
    {
        var key = e.Key;

        if (key == "F11")
        {
            _toggleDebugStatsState();

        }
        else if (key == "F12")
        {
            ToggleMonitor();
        }
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

    /// <summary>
    /// </summary>
    /// <param name="e"></param>
    public void OnKeyPress(KeyboardEventArgs e)
    {
    }
}
