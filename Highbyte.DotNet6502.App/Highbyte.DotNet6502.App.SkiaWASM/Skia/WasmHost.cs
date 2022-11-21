using Highbyte.DotNet6502.App.SkiaWASM.Instrumentation.Stats;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SkiaSharp;
using System.Web;

namespace Highbyte.DotNet6502.App.SkiaWASM.Skia;

public class WasmHost : IDisposable
{
    public bool Initialized { get; private set; }

    private readonly IJSRuntime _jsRuntime;

    private SystemRunner _systemRunner;

    private SKCanvas _skCanvas;
    private GRContext _grContext;
    private SkiaRenderContext _skiaRenderContext;
    private PeriodicAsyncTimer? _updateTimer;

    public AspNetInputHandlerContext InputHandlerContext { get; private set; }
    private readonly ISystem _system;
    private readonly Func<ISystem, SkiaRenderContext, AspNetInputHandlerContext, Task<SystemRunner>> _getSystemRunner;
    private readonly Action<string> _updateStats;
    private readonly Action<string> _updateDebug;
    private readonly Func<bool, Task> _setMonitorState;
    private readonly MonitorConfig _monitorConfig;
    private readonly Func<Task> _toggleDebugStatsState;
    private readonly float _scale;

    public WasmMonitor Monitor { get; private set; }

    private readonly ElapsedMillisecondsTimedStat _inputTime;
    private readonly ElapsedMillisecondsTimedStat _systemTime;
    private readonly ElapsedMillisecondsTimedStat _renderTime;
    private readonly PerSecondTimedStat _updateFps;
    private readonly PerSecondTimedStat _renderFps;

    private const int STATS_EVERY_X_FRAME = 60 * 1;
    private int _statsFrameCount = 0;

    private const int DEBUGMESSAGE_EVERY_X_FRAME = 1;
    private int _debugFrameCount = 0;

    public WasmHost(
        IJSRuntime jsRuntime,
        ISystem system,
        Func<ISystem, SkiaRenderContext, AspNetInputHandlerContext, Task<SystemRunner>> getSystemRunner,
        Action<string> updateStats,
        Action<string> updateDebug,
        Func<bool, Task> setMonitorState,
        MonitorConfig monitorConfig,
        Func<Task> toggleDebugStatsState,
        float scale = 1.0f)
    {
        _jsRuntime = jsRuntime;
        _system = system;
        _getSystemRunner = getSystemRunner;
        _updateStats = updateStats;
        _updateDebug = updateDebug;
        _setMonitorState = setMonitorState;
        _monitorConfig = monitorConfig;
        _toggleDebugStatsState = toggleDebugStatsState;
        _scale = scale;

        // Init stats
        InstrumentationBag.Clear();
        _inputTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("WASM-InputTime");
        _systemTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("Emulator-SystemTime");
        _renderTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("WASMSkiaSharp-RenderTime");
        _updateFps = InstrumentationBag.Add<PerSecondTimedStat>("WASMSkiaSharp-OnUpdateFPS");
        _renderFps = InstrumentationBag.Add<PerSecondTimedStat>("WASMSkiaSharp-OnRenderFPS");

        Initialized = false;
    }

    public async Task Init(SKCanvas canvas, GRContext grContext)
    {
        _skCanvas = canvas;
        _grContext = grContext;

        _skiaRenderContext = new SkiaRenderContext(GetCanvas, GetGRContext);
        InputHandlerContext = new AspNetInputHandlerContext();
        _systemRunner = await _getSystemRunner(_system, _skiaRenderContext, InputHandlerContext);

        Monitor = new WasmMonitor(_jsRuntime, _systemRunner, _monitorConfig, _setMonitorState);

        Initialized = true;
    }

    public void Stop()
    {
        if (_updateTimer == null)
            return;
        _updateTimer!.Stop();
    }

    public void Start()
    {
        if (_updateTimer != null)
        {
        }
        else
        {
            var screen = (IScreen)_system;
            // Number of milliseconds between each invokation of the main loop. 60 fps -> (1/60) * 1000  -> approx 16.6667ms
            double updateIntervalMS = (1 / screen.RefreshFrequencyHz) * 1000;
            _updateTimer = new PeriodicAsyncTimer();
            _updateTimer.IntervalMilliseconds = updateIntervalMS;
            _updateTimer.Elapsed += UpdateTimerElapsed;
        }

        _updateTimer!.Start();
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

        // Cleanup Skia resources
        _skiaRenderContext?.Cleanup();

        // Cleanup input handler resources
        InputHandlerContext?.Cleanup();
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
            var debugString = GetDebugMessage();
            _updateDebug(debugString);
        }

        //_emulatorHelper.GenerateRandomNumber();
        using (_inputTime.Measure())
        {
            _systemRunner.ProcessInput();
        }

        bool cont;
        using (_systemTime.Measure())
        {
            cont = _systemRunner.RunEmulatorOneFrame();
        }

        _statsFrameCount++;
        if (_statsFrameCount >= STATS_EVERY_X_FRAME)
        {
            _statsFrameCount = 0;
            var statsString = GetStats();
            _updateStats(statsString);
        }

        // Show monitor if we encounter breakpoint or other break
        if (!cont)
            Monitor.Enable();
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

    private string GetDebugMessage()
    {
        string msg = "DEBUG: ";
        msg += _systemRunner.InputHandler.GetDebugMessage();
        return BuildHtmlString(msg, "header");
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
