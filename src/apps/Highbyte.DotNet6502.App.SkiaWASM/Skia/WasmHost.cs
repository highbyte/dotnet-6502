using Highbyte.DotNet6502.App.SkiaWASM.Instrumentation.Stats;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Toolbelt.Blazor.Gamepad;

namespace Highbyte.DotNet6502.App.SkiaWASM.Skia;

public class WasmHost : IDisposable
{
    public bool Initialized { get; private set; }

    private readonly IJSRuntime _jsRuntime;

    private PeriodicAsyncTimer? _updateTimer;

    private SystemRunner _systemRunner;
    public SystemRunner SystemRunner => _systemRunner;

    private SKCanvas _skCanvas;
    private GRContext _grContext;

    private SkiaRenderContext _skiaRenderContext;
    public WASMAudioHandlerContext AudioHandlerContext { get; private set; }
    public AspNetInputHandlerContext InputHandlerContext { get; private set; }

    private readonly string _systemName;
    private readonly SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext> _systemList;
    public SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext> SystemList => _systemList;
    private readonly Action<string> _updateStats;
    private readonly Action<string> _updateDebug;
    private readonly Func<bool, Task> _setMonitorState;
    private readonly MonitorConfig _monitorConfig;
    private readonly Func<Task> _toggleDebugStatsState;
    private readonly ILoggerFactory _loggerFactory;
    private readonly float _scale;
    private readonly float _initialMasterVolume;
    private readonly ILogger _logger;

    public WasmMonitor Monitor { get; private set; }

    private readonly ElapsedMillisecondsTimedStat _inputTime;
    private readonly ElapsedMillisecondsTimedStat _systemTime;
    private readonly ElapsedMillisecondsStat _systemTimeAudio;  // Part of systemTime, but we want to show it separately
    private readonly ElapsedMillisecondsTimedStat _renderTime;
    private readonly PerSecondTimedStat _updateFps;
    private readonly PerSecondTimedStat _renderFps;

    private const string CustomSystemStatNamePrefix = "Emulator-SystemTime-Custom-";
    private Dictionary<string, ElapsedMillisecondsStat> _customSystemStats = new();


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
        MonitorConfig monitorConfig,
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
        _monitorConfig = monitorConfig;
        _toggleDebugStatsState = toggleDebugStatsState;
        _scale = scale;
        _initialMasterVolume = initialMasterVolume;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(WasmHost).Name);

        // Init stats
        InstrumentationBag.Clear();
        _inputTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("WASM-InputTime");

        _systemTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("Emulator-SystemTime");
        _systemTimeAudio = InstrumentationBag.Add<ElapsedMillisecondsStat>("Emulator-SystemTime-Audio");

        _renderTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("WASMSkiaSharp-RenderTime");
        _updateFps = InstrumentationBag.Add<PerSecondTimedStat>("WASMSkiaSharp-OnUpdateFPS");
        _renderFps = InstrumentationBag.Add<PerSecondTimedStat>("WASMSkiaSharp-OnRenderFPS");

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

        Monitor = new WasmMonitor(_jsRuntime, _systemRunner, _monitorConfig, _setMonitorState);

        var system = await _systemList.GetSystem(_systemName);
        InitCustomSystemStats(system);

        Initialized = true;
    }

    public void Stop()
    {
        _updateTimer?.Stop();

        _systemRunner.AudioHandler.PausePlaying();
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
    }

    public void InitCustomSystemStats(ISystem system)
    {
        // Remove any existing custom system stats
        foreach (var existingCustomSystemStatName in _customSystemStats.Keys)
        {
            if (existingCustomSystemStatName.StartsWith(CustomSystemStatNamePrefix))
            {
                InstrumentationBag.Remove(existingCustomSystemStatName);
                _customSystemStats.Remove(existingCustomSystemStatName);
            }
        }
        // Add any custom system stats for selected system
        foreach (var customSystemStatName in system.DetailedStatNames)
        {
            _customSystemStats.Add($"{CustomSystemStatNamePrefix}{customSystemStatName}", InstrumentationBag.Add<ElapsedMillisecondsStat>($"{CustomSystemStatNamePrefix}{customSystemStatName}"));
        }
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
        using (_inputTime.Measure())
        {
            _systemRunner.ProcessInput();
        }

        ExecEvaluatorTriggerResult execEvaluatorTriggerResult;
        using (_systemTime.Measure())
        {
            execEvaluatorTriggerResult = _systemRunner.RunEmulatorOneFrame(out Dictionary<string, double> detailedStats);

            if (detailedStats.ContainsKey("Audio"))
            {
                _systemTimeAudio.Set(detailedStats["Audio"]);
                _systemTimeAudio.UpdateStat();
            }

            // Update custom system stats
            // TODO: Make custom system stats less messy?
            foreach (var detailedStatName in detailedStats.Keys)
            {
                var statLookup = _customSystemStats.Keys.SingleOrDefault(x => x.EndsWith(detailedStatName));
                if (statLookup != null)
                {
                    _customSystemStats[$"{CustomSystemStatNamePrefix}{detailedStatName}"].Set(detailedStats[detailedStatName]);
                    _customSystemStats[$"{CustomSystemStatNamePrefix}{detailedStatName}"].UpdateStat();
                }
            }
        }

        _statsFrameCount++;
        if (_statsFrameCount >= STATS_EVERY_X_FRAME)
        {
            _statsFrameCount = 0;
            var statsString = GetStats();
            _updateStats(statsString);
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
        _skCanvas.Scale(_scale);
        using (_renderTime.Measure())
        {
            _systemRunner.Draw();
            //using (new SKAutoCanvasRestore(skCanvas))
            //{
            //    _systemRunner.Draw(skCanvas);
            //}
        }
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

        foreach ((string name, IStat stat) in InstrumentationBag.Stats.OrderBy(i => i.Name))
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

        var inputStats = _systemRunner.InputHandler.GetStats();
        var inputStatsOneString = string.Join(" # ", inputStats);
        debugMessages += $"{BuildHtmlString("INPUT", "header")}: {BuildHtmlString(inputStatsOneString, "value")} ";
        //foreach (var message in inputStats)
        //{
        //    if (debugMessages != "")
        //        debugMessages += "<br />";
        //    debugMessages += $"{BuildHtmlString("DEBUG INPUT", "header")}: {BuildHtmlString(message, "value")} ";
        //}

        var audioStats = _systemRunner.AudioHandler.GetStats();
        foreach (var message in audioStats)
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
    public async Task OnKeyDown(KeyboardEventArgs e)
    {
        var key = e.Key;

        if (key == "F11")
        {
            await _toggleDebugStatsState();

        }
        else if (key == "F12")
        {
            await ToggleMonitor();
        }
    }

    public async Task ToggleMonitor()
    {
        if (Monitor.Visible)
        {
            await Monitor.Disable();
        }
        else
        {
            await Monitor.Enable();
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="e"></param>
    public void OnKeyPress(KeyboardEventArgs e)
    {
    }
}
