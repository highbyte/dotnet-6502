using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems;
using Toolbelt.Blazor.Gamepad;

namespace Highbyte.DotNet6502.App.WASM.Emulator;

public abstract class WasmHostBase : IDisposable
{
    private string _systemName;

    private readonly IJSRuntime _jsRuntime;


    private SystemRunner _systemRunner = default!;
    public SystemRunner SystemRunner => _systemRunner;

    public EmulatorState EmulatorState { get; private set; } = EmulatorState.Uninitialized;

    public WASMRenderContextContainer RenderContextContainer { get; private set; } = default!;
    public WASMAudioHandlerContext AudioHandlerContext { get; private set; } = default!;
    public AspNetInputHandlerContext InputHandlerContext { get; private set; } = default!;

    private readonly SystemList<WASMRenderContextContainer, AspNetInputHandlerContext, WASMAudioHandlerContext> _systemList;
    public SystemList<WASMRenderContextContainer, AspNetInputHandlerContext, WASMAudioHandlerContext> SystemList => _systemList;

    protected EmulatorConfig EmulatorConfig => _emulatorConfig;


    private readonly Action<string> _updateStats;
    private readonly Action<string> _updateDebug;
    private readonly Func<bool, Task> _setMonitorState;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly Func<Task> _toggleDebugStatsState;
    private readonly ILoggerFactory _loggerFactory;
    private AudioContextSync _audioContext;
    private readonly GamepadList _gamepadList;
    private readonly float _initialMasterVolume;
    private readonly ILogger _logger;

    public WasmMonitor Monitor { get; private set; } = default!;

    private const string HostStatRootName = "WASM";
    private const string SystemTimeStatName = "Emulator-SystemTime";
    private const string RenderTimeStatName = "RenderTime";
    private const string InputTimeStatName = "InputTime";
    private const string AudioTimeStatName = "AudioTime";
    private readonly ElapsedMillisecondsTimedStat _systemTime;
    private readonly ElapsedMillisecondsTimedStat _renderTime;
    private readonly ElapsedMillisecondsTimedStat _inputTime;
    private readonly PerSecondTimedStat _updateFps;
    private readonly PerSecondTimedStat _renderFps;

    private const int STATS_EVERY_X_FRAME = 60 * 1;
    private int _statsFrameCount = 0;

    private const int DEBUGMESSAGE_EVERY_X_FRAME = 1;
    private int _debugFrameCount = 0;

    public WasmHostBase(
        IJSRuntime jsRuntime,
        SystemList<WASMRenderContextContainer, AspNetInputHandlerContext, WASMAudioHandlerContext> systemList,
        Action<string> updateStats,
        Action<string> updateDebug,
        Func<bool, Task> setMonitorState,
        EmulatorConfig emulatorConfig,
        Func<Task> toggleDebugStatsState,
        ILoggerFactory loggerFactory,
        GamepadList gamepadList,
        float initialMasterVolume = 50.0f)
    {
        _jsRuntime = jsRuntime;
        _systemList = systemList;
        _updateStats = updateStats;
        _updateDebug = updateDebug;
        _setMonitorState = setMonitorState;
        _emulatorConfig = emulatorConfig;
        _toggleDebugStatsState = toggleDebugStatsState;
        _gamepadList = gamepadList;
        _initialMasterVolume = initialMasterVolume;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(WasmHostBase).Name);

        // Init stats
        InstrumentationBag.Clear();
        _systemTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>($"{HostStatRootName}-{SystemTimeStatName}");
        _inputTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>($"{HostStatRootName}-{InputTimeStatName}");
        //_audioTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>($"{HostStatRootName}-{AudioTimeStatName}");
        _renderTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>($"{HostStatRootName}-{RenderTimeStatName}");
        _updateFps = InstrumentationBag.Add<PerSecondTimedStat>($"{HostStatRootName}-OnUpdateFPS");
        _renderFps = InstrumentationBag.Add<PerSecondTimedStat>($"{HostStatRootName}-OnRenderFPS");

    }

    public void Init(AudioContextSync audioContext)
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            throw new DotNet6502Exception("Internal error. Cannot init emulator if not in uninitialized state.");

        _audioContext = audioContext;

        RenderContextContainer = BuildRenderContext();
        InputHandlerContext = new AspNetInputHandlerContext(_loggerFactory, _gamepadList);
        AudioHandlerContext = new WASMAudioHandlerContext(_audioContext, _jsRuntime, _initialMasterVolume);
        _systemList.InitContext(() => RenderContextContainer, () => InputHandlerContext, () => AudioHandlerContext);
    }

    protected abstract WASMRenderContextContainer BuildRenderContext();

    private async Task InitSystem()
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            throw new DotNet6502Exception("Internal error. Cannot init emulator system if not in uninitialized state.");

        _systemRunner = await _systemList.BuildSystemRunner(_systemName);
        Monitor = new WasmMonitor(_jsRuntime, _systemRunner, _emulatorConfig, _setMonitorState);
        OnAfterInitSystem();
    }

    protected virtual void OnAfterInitSystem() { }

    public async Task Start()
    {
        if (EmulatorState == EmulatorState.Running)
            return;

        if (!_systemList.IsValidConfig(_systemName).Result)
            throw new DotNet6502Exception("Internal error. Cannot start emulator if current system config is invalid.");

        // Only create a new instance of SystemRunner if we previously has not started (so resume after pause works).
        if (EmulatorState == EmulatorState.Uninitialized)
        {
            await InitSystem();
        }

        if (_systemRunner != null && _systemRunner.AudioHandler != null)
            _systemRunner.AudioHandler.StartPlaying();

        EmulatorState = EmulatorState.Running;

        _logger.LogInformation($"System started: {_systemName}");

        OnAfterStart();
    }

    protected virtual void OnAfterStart() { }

    public void Pause()
    {
        if (EmulatorState == EmulatorState.Paused || EmulatorState == EmulatorState.Uninitialized)
            return;

        _systemRunner.AudioHandler?.PausePlaying();

        EmulatorState = EmulatorState.Paused;

        _logger.LogInformation($"System paused: {_systemName}");

        OnAfterPause();
    }

    protected virtual void OnAfterPause() { }

    public async Task Reset()
    {
        if (EmulatorState == EmulatorState.Uninitialized)
            return;

        Stop();

        await Start();

        OnAfterReset();
    }

    protected virtual void OnAfterReset() { }

    public void Stop()
    {
        if (EmulatorState == EmulatorState.Running)
            Pause();

        _systemRunner = default!;
        EmulatorState = EmulatorState.Uninitialized;

        _logger.LogInformation($"System stopped: {_systemName}");

        OnAfterStop();
    }
    protected virtual void OnAfterStop() { }


    public async Task SetCurrentSystem(string systemName)
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            throw new DotNet6502Exception("Internal error. Cannot change system while running");

        if (!_systemList.IsValidConfig(systemName).Result)
        {
            throw new Exception($"Invalid system config: {systemName}");
        }

        _systemName = systemName;
        _logger.LogInformation($"System selected: {_systemName}");
    }

    protected void Render()
    {
        if (EmulatorState != EmulatorState.Running)
            return;

        if (Monitor.Visible)
            return;

        _renderFps.Update();

        OnBeforeRender();

        using (_renderTime.Measure())
        {
            _systemRunner.Draw();
        }
    }

    protected virtual void OnBeforeRender() { }

    public virtual void Cleanup()
    {
        // Clean up render context resources
        RenderContextContainer?.Cleanup();

        // Clean up input handler resources
        InputHandlerContext?.Cleanup();

        // Stop any playing audio
        _systemRunner.AudioHandler?.StopPlaying();
        // Clean up audio resources
        //AudioHandlerContext?.Cleanup();

        OnAfterCleanup();
    }

    protected virtual void OnAfterCleanup() { }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    protected void EmulatorRunOneFrame()
    {
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
            execEvaluatorTriggerResult = _systemRunner.RunEmulatorOneFrame();
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
            Monitor.Enable(execEvaluatorTriggerResult);
    }

    private string GetStats()
    {
        var stats = "";

        var allStats = InstrumentationBag.Stats
            .Union(_systemRunner.System.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{SystemTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.Renderer.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{RenderTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.AudioHandler.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{AudioTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.InputHandler.Instrumentations.Stats.Select(x => (Name: $"{HostStatRootName}-{InputTimeStatName}-{x.Name}", x.Stat)))
            .ToList();
        foreach ((var name, var stat) in allStats.OrderBy(i => i.Name))
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
        var debugMessages = "";

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
        var html = "";
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
            _toggleDebugStatsState();
        else if (key == "F12")
        {
            ToggleMonitor();
        }
    }

    public void ToggleMonitor()
    {
        if (Monitor.Visible)
            Monitor.Disable();
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

public enum EmulatorState
{
    Uninitialized,
    Running,
    Paused
}
