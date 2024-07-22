using Highbyte.DotNet6502.Instrumentation.Stats;
using Highbyte.DotNet6502.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems;

public enum EmulatorState { Uninitialized, Running, Paused }

/// <summary>
/// A base class to be used as a common host application model for managing and running different emulators on a specific host platform.
/// The generic type parameters TRenderContext, TInputHandlerContext, and TAudioHandlerContext dictates the types of rendering, input handling, and audio handling available on a specific platform.
/// 
/// The constructor must also provide a generic SystemList parameter (with the same generic context types) that provides the different emulators and their Renderers, InputHandlers, and AudioHandlers base on the context types.
/// </summary>
/// <typeparam name="TRenderContext"></typeparam>
/// <typeparam name="TInputHandlerContext"></typeparam>
/// <typeparam name="TAudioHandlerContext"></typeparam>
public class HostApp<TRenderContext, TInputHandlerContext, TAudioHandlerContext>
    where TRenderContext : IRenderContext
    where TInputHandlerContext : IInputHandlerContext
    where TAudioHandlerContext : IAudioHandlerContext
{
    // Injected via constructor
    private readonly ILogger _logger;
    private readonly SystemList<TRenderContext, TInputHandlerContext, TAudioHandlerContext> _systemList;
    private readonly Dictionary<string, IHostSystemConfig> _hostSystemConfigs;

    // Other variables
    private string _selectedSystemName;
    public string SelectedSystemName => _selectedSystemName;
    public HashSet<string> AvailableSystemNames => _systemList.Systems;

    private SystemRunner? _systemRunner = null;
    public SystemRunner? CurrentSystemRunner => _systemRunner;
    public ISystem? CurrentRunningSystem => _systemRunner?.System;
    public EmulatorState EmulatorState { get; private set; } = EmulatorState.Uninitialized;

    private readonly string _hostName;
    private const string SystemTimeStatName = "Emulator-SystemTime";
    private const string RenderTimeStatName = "RenderTime";
    private const string InputTimeStatName = "InputTime";
    private const string AudioTimeStatName = "AudioTime";
    private readonly Instrumentations _systemInstrumentations = new();
    private ElapsedMillisecondsTimedStatSystem? _systemTime;
    private ElapsedMillisecondsTimedStatSystem? _renderTime;
    private ElapsedMillisecondsTimedStatSystem? _inputTime;
    //private ElapsedMillisecondsTimedStatSystem _audioTime;

    private readonly PerSecondTimedStat _updateFps;
    private readonly PerSecondTimedStat _renderFps;


    public HostApp(
        string hostName,
        SystemList<TRenderContext, TInputHandlerContext, TAudioHandlerContext> systemList,
        Dictionary<string, IHostSystemConfig> hostSystemConfigs,
        ILoggerFactory loggerFactory
        )
    {
        _hostName = hostName;
        _updateFps = InstrumentationBag.Add<PerSecondTimedStat>($"{_hostName}-OnUpdateFPS");
        _renderFps = InstrumentationBag.Add<PerSecondTimedStat>($"{_hostName}-OnRenderFPS");

        _logger = loggerFactory.CreateLogger("HostApp");

        if (systemList.Systems.Count == 0)
            throw new DotNet6502Exception("No systems added to system list.");
        _systemList = systemList;
        _selectedSystemName = _systemList.Systems.First();

        _hostSystemConfigs = hostSystemConfigs;
    }

    public void SetContexts(
        Func<TRenderContext>? getRenderContext = null,
        Func<TInputHandlerContext>? getInputHandlerContext = null,
        Func<TAudioHandlerContext>? getAudioHandlerContext = null
        )
    {
        _systemList.SetContext(getRenderContext, getInputHandlerContext, getAudioHandlerContext);
    }

    public void InitRenderContext() => _systemList.InitRenderContext();
    public void InitInputHandlerContext() => _systemList.InitInputHandlerContext();
    public void InitAudioHandlerContext() => _systemList.InitAudioHandlerContext();

    public bool IsRenderContextInitialized => _systemList.IsRenderContextInitialized;
    public bool IsInputHandlerContextInitialized => _systemList.IsInputHandlerContextInitialized;
    public bool IsAudioHandlerContextInitialized => _systemList.IsAudioHandlerContextInitialized;


    public void SelectSystem(string systemName)
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            throw new DotNet6502Exception("Cannot change system while emulator is running.");
        if (!_systemList.Systems.Contains(systemName))
            throw new DotNet6502Exception($"System not found: {systemName}");
        _selectedSystemName = systemName;
    }
    public virtual void OnAfterSelectSystem() { }

    public virtual bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        return true;
    }

    public async Task Start()
    {
        if (EmulatorState == EmulatorState.Running)
            return;

        if (!_systemList.IsValidConfig(_selectedSystemName).Result)
            throw new DotNet6502Exception("Internal error. Cannot start emulator if current system config is invalid.");

        // Force a full GC to free up memory, so it won't risk accumulate memory usage if GC has not run for a while.
        var m0 = GC.GetTotalMemory(forceFullCollection: true);
        _logger.LogInformation("Allocated memory before starting emulator: " + m0);

        var systemAboutToBeStarted = await _systemList.GetSystem(_selectedSystemName);
        bool shouldStart = OnBeforeStart(systemAboutToBeStarted);
        if (!shouldStart)
            return;

        var emulatorStateBeforeStart = EmulatorState;
        // Only create a new instance of SystemRunner if we previously has not started (so resume after pause works).
        if (EmulatorState == EmulatorState.Uninitialized)
            _systemRunner = _systemList.BuildSystemRunner(_selectedSystemName).Result;

        InitInstrumentation(_systemRunner!.System);

        _systemRunner.AudioHandler.StartPlaying();

        OnAfterStart(emulatorStateBeforeStart);

        EmulatorState = EmulatorState.Running;
        _logger.LogInformation($"System started: {_selectedSystemName}");

    }
    public virtual void OnAfterStart(EmulatorState emulatorStateBeforeStart) { }

    public void Pause()
    {
        if (EmulatorState == EmulatorState.Paused || EmulatorState == EmulatorState.Uninitialized)
            return;

        _systemRunner!.AudioHandler.PausePlaying();

        OnAfterPause();

        EmulatorState = EmulatorState.Paused;
        _logger.LogInformation($"System paused: {_selectedSystemName}");
    }

    public virtual void OnAfterPause() { }

    public void Stop()
    {
        if (EmulatorState == EmulatorState.Running)
            Pause();

        _systemRunner!.AudioHandler.StopPlaying();

        // Cleanup systemrunner (which also cleanup renderer, inputhandler, and audiohandler)
        _systemRunner!.Cleanup();
        _systemRunner = default!;

        OnAfterStop();

        EmulatorState = EmulatorState.Uninitialized;
        _logger.LogInformation($"System stopped: {_selectedSystemName}");
    }
    public virtual void OnAfterStop() { }

    public async Task Reset()
    {
        if (EmulatorState == EmulatorState.Uninitialized)
            return;

        Stop();
        await Start();
    }

    public void Close()
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            Stop();

        _logger.LogInformation($"Emulator closed");

        OnAfterClose();
    }
    public virtual void OnAfterClose() { }


    public virtual void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = true;
        shouldReceiveInput = true;
    }
    public void RunEmulatorOneFrame()
    {
        // Safety check to avoid running emulator if it's not in a running state.
        if (EmulatorState != EmulatorState.Running)
            return;

        OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput);
        if (!shouldRun)
            return;

        _updateFps.Update();

        if (shouldReceiveInput)
        {
            _inputTime!.Start();
            _systemRunner!.ProcessInputBeforeFrame();
            _inputTime!.Stop();
        }

        _systemTime!.Start();
        var execEvaluatorTriggerResult = _systemRunner!.RunEmulatorOneFrame();
        OnAfterRunEmulatorOneFrame(execEvaluatorTriggerResult);
        _systemTime!.Stop();

    }
    public virtual void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult) { }

    public virtual void OnBeforeDrawFrame(bool emulatorWillBeRendered) { }

    public void DrawFrame()
    {
        _renderFps.Update();

        if (EmulatorState != EmulatorState.Running)
        {
            OnBeforeDrawFrame(emulatorWillBeRendered: false);
            OnAfterDrawFrame(emulatorRendered: false);
            return;
        }

        _renderTime!.Start();
        OnBeforeDrawFrame(emulatorWillBeRendered: true);
        _systemRunner!.Draw();
        OnAfterDrawFrame(emulatorRendered: true);
        _renderTime!.Stop();
    }
    public virtual void OnAfterDrawFrame(bool emulatorRendered) { }

    public async Task<bool> IsSystemConfigValid()
    {
        return await _systemList.IsValidConfig(_selectedSystemName);
    }
    public async Task<(bool, List<string> validationErrors)> IsValidConfigWithDetails()
    {
        return await _systemList.IsValidConfigWithDetails(_selectedSystemName);
    }

    public async Task<ISystem> GetSelectedSystem()
    {
        return await _systemList.GetSystem(_selectedSystemName);
    }

    public async Task<ISystemConfig> GetSystemConfig()
    {
        return await _systemList.GetCurrentSystemConfig(_selectedSystemName);
    }
    public IHostSystemConfig GetHostSystemConfig()
    {
        return _hostSystemConfigs[_selectedSystemName];
    }

    public void UpdateSystemConfig(ISystemConfig newConfig)
    {
        _systemList.ChangeCurrentSystemConfig(_selectedSystemName, newConfig);
    }

    public async Task PersistNewSystemConfig(ISystemConfig newConfig)
    {
        await _systemList.PersistNewSystemConfig(_selectedSystemName, newConfig);
    }


    private void InitInstrumentation(ISystem system)
    {
        _systemInstrumentations.Clear();
        _systemTime = _systemInstrumentations.Add($"{_hostName}-{SystemTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
        _renderTime = _systemInstrumentations.Add($"{_hostName}-{RenderTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
        _inputTime = _systemInstrumentations.Add($"{_hostName}-{InputTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
        //_audioTime = InstrumentationBag.Add($"{HostStatRootName}-{AudioTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
    }

    public List<(string name, IStat stat)> GetStats()
    {
        if (_systemRunner == null)
            return new List<(string name, IStat)>();

        return InstrumentationBag.Stats
            .Union(_systemInstrumentations.Stats)
            .Union(_systemRunner.System.Instrumentations.Stats.Select(x => (Name: $"{_hostName}-{SystemTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.Renderer.Instrumentations.Stats.Select(x => (Name: $"{_hostName}-{RenderTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.AudioHandler.Instrumentations.Stats.Select(x => (Name: $"{_hostName}-{AudioTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.InputHandler.Instrumentations.Stats.Select(x => (Name: $"{_hostName}-{InputTimeStatName}-{x.Name}", x.Stat)))
            .ToList();
    }
}
