using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

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
public class HostApp<TRenderContext, TInputHandlerContext, TAudioHandlerContext> : IHostApp
    where TRenderContext : IRenderContext
    where TInputHandlerContext : IInputHandlerContext
    where TAudioHandlerContext : IAudioHandlerContext
{
    // Injected via constructor
    private readonly ILogger _logger;
    private readonly SystemList<TRenderContext, TInputHandlerContext, TAudioHandlerContext> _systemList;

    // Other variables
    private string _selectedSystemName;
    public string SelectedSystemName => _selectedSystemName;
    private ISystem? _selectedSystemTemporary; // A temporary storage of the selected system if asked for, and system has not been started yet.

    public HashSet<string> AvailableSystemNames => _systemList.Systems;

    private string _selectedSystemConfigurationVariant;
    public string SelectedSystemConfigurationVariant => _selectedSystemConfigurationVariant;
    private List<string> _allSelectedSystemConfigurationVariants = new();
    public List<string> AllSelectedSystemConfigurationVariants => _allSelectedSystemConfigurationVariants;


    private SystemRunner? _systemRunner = null;
    public SystemRunner? CurrentSystemRunner => _systemRunner;
    public ISystem? CurrentRunningSystem => _systemRunner?.System;
    public EmulatorState EmulatorState { get; private set; } = EmulatorState.Uninitialized;

    private IHostSystemConfig? _currentHostSystemConfig;

    /// <summary>
    /// The current host system config.
    /// </summary>
    public IHostSystemConfig CurrentHostSystemConfig
    {
        get
        {
            if (_currentHostSystemConfig == null)
                throw new DotNet6502Exception("Internal error. No system selected yet. Call SelectSystem() first.");
            return _currentHostSystemConfig;
        }
        private set
        {
            _currentHostSystemConfig = value;
        }
    }

    protected List<IHostSystemConfig> GetHostSystemConfigs()
    {
        var list = new List<IHostSystemConfig>();
        foreach (var system in AvailableSystemNames)
        {
            list.Add(_systemList.GetHostSystemConfig(system).Result);
        }
        return list;
    }

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

    private readonly Instrumentations _instrumentations = new();
    private readonly PerSecondTimedStat _updateFps;
    private readonly PerSecondTimedStat _renderFps;

    public HostApp(
        string hostName,
        SystemList<TRenderContext, TInputHandlerContext, TAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory
        )
    {
        _hostName = hostName;
        _updateFps = _instrumentations.Add($"{_hostName}-OnUpdateFPS", new PerSecondTimedStat());
        _renderFps = _instrumentations.Add($"{_hostName}-OnRenderFPS", new PerSecondTimedStat());

        _logger = loggerFactory.CreateLogger("HostApp");

        if (systemList.Systems.Count == 0)
            throw new DotNet6502Exception("No systems added to system list.");
        _systemList = systemList;

        //Note: Because selecting a system (incl which variants it has) requires async call,
        //      call SelectSystem(string systemName) after HostApp is created to set the initial system.
        _selectedSystemName = "DEFAULT SYSTEM";
        _allSelectedSystemConfigurationVariants = new List<string> { "DEFAULT VARIANT" };
        _selectedSystemConfigurationVariant = _allSelectedSystemConfigurationVariants.First();
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

    public async Task SelectSystem(string systemName)
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            throw new DotNet6502Exception("Cannot change system while emulator is running.");
        if (!_systemList.Systems.Contains(systemName))
            throw new DotNet6502Exception($"System not found: {systemName}");

        bool systemChanged = systemName != _selectedSystemName;

        _selectedSystemName = systemName;
        CurrentHostSystemConfig = await _systemList.GetHostSystemConfig(_selectedSystemName);

        if (systemChanged)
        {
            _allSelectedSystemConfigurationVariants = await _systemList.GetSystemConfigurationVariants(systemName, CurrentHostSystemConfig);
            _selectedSystemConfigurationVariant = _allSelectedSystemConfigurationVariants.First();
        }

        // Make sure any state regarding the system variant is also in sync
        await SelectSystemConfigurationVariant(_selectedSystemConfigurationVariant);

        OnAfterSelectSystem();
    }

    public async Task SelectSystemConfigurationVariant(string configurationVariant)
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            throw new DotNet6502Exception("Cannot change system while emulator is running.");
        var configVariants = await _systemList.GetSystemConfigurationVariants(_selectedSystemName, CurrentHostSystemConfig);
        if (!configVariants.Contains(configurationVariant))
            throw new DotNet6502Exception($"System configuration variant not found: {configurationVariant}");

        _selectedSystemConfigurationVariant = configurationVariant;

        // Pre-create a temporary variable to contain the system if it is valid.
        // This is useful if the system has not been started yet, but client requests the system object.
        if (CurrentHostSystemConfig.IsValid(out _))
        {
            _selectedSystemTemporary = await _systemList.BuildSystem(_selectedSystemName, _selectedSystemConfigurationVariant);
        }
        else
        {
            _selectedSystemTemporary = null;
        }
    }

    public virtual void OnAfterSelectSystem() { }

    public virtual bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        return true;
    }

    public async Task Start()
    {
        if (EmulatorState == EmulatorState.Running)
            throw new DotNet6502Exception("Cannot start emulator if emulator is running.");

        if (!await _systemList.IsValidConfig(_selectedSystemName))
            throw new DotNet6502Exception("Cannot start emulator if current system config is invalid.");

        ISystem systemToBeStarted;
        if (EmulatorState == EmulatorState.Uninitialized)
        {
            if (_selectedSystemTemporary == null)
            {
                // If we don't have a temporary system, build it.
                _selectedSystemTemporary = await _systemList.BuildSystem(_selectedSystemName, _selectedSystemConfigurationVariant);
            }
            systemToBeStarted = _selectedSystemTemporary;
        }
        else
        {
            // We already have a system runner, so use the system from that.
            systemToBeStarted = _systemRunner!.System;
        }

        bool shouldStart = OnBeforeStart(systemToBeStarted);
        if (!shouldStart)
            return;

        var emulatorStateBeforeStart = EmulatorState;

        // Only create a new instance of SystemRunner if we previously has not started (so resume after pause works).
        if (EmulatorState == EmulatorState.Uninitialized)
            _systemRunner = await _systemList.BuildSystemRunner(systemToBeStarted);

        InitInstrumentation(_systemRunner!.System);

        _systemRunner.AudioHandler.StartPlaying();

        OnAfterStart(emulatorStateBeforeStart);

        EmulatorState = EmulatorState.Running;
        _logger.LogInformation($"System started: {_selectedSystemName} Variant: {_selectedSystemConfigurationVariant}");

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

    public virtual void OnBeforeStop() { }

    public void Stop()
    {
        if (EmulatorState == EmulatorState.Running)
            Pause();

        OnBeforeStop();

        _systemRunner!.AudioHandler.StopPlaying();

        // Cleanup systemrunner (which also cleanup renderer, inputhandler, and audiohandler)
        _systemRunner!.Cleanup();
        _systemRunner = default!;

        EmulatorState = EmulatorState.Uninitialized;
        _selectedSystemTemporary = null; // Clear the temporary system, as it is no longer valid.

        OnAfterStop();

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

    public async Task<bool> IsAudioSupported()
    {
        return await _systemList.IsAudioSupported(_selectedSystemName);
    }

    public async Task<bool> IsAudioEnabled()
    {
        return await _systemList.IsAudioEnabled(_selectedSystemName);
    }
    public async Task SetAudioEnabled(bool enabled)
    {
        await _systemList.SetAudioEnabled(_selectedSystemName, enabled: enabled);
    }

    public async Task<ISystem> GetSelectedSystem()
    {
        if (EmulatorState == EmulatorState.Uninitialized)
        {
            // If we haven't started started the selected system yet, return a temporary instance of the system (set in SelectSystem method).
            if (_selectedSystemTemporary == null)
                throw new DotNet6502Exception("Internal state error.");
            return _selectedSystemTemporary;
        }
        // The emulator is running, return the current system runner's system.
        return _systemRunner!.System;
    }

    public void UpdateHostSystemConfig(IHostSystemConfig newConfig)
    {
        // Note: Make sure to store a clone of the newConfig in the systemList, so it cannot be changed by the caller (bound to UI for example).
        CurrentHostSystemConfig = (IHostSystemConfig)newConfig.Clone();
        _systemList.ChangeCurrentHostSystemConfig(_selectedSystemName, CurrentHostSystemConfig);

        //Re-select the system to ensure the new config is applied.
        SelectSystem(_selectedSystemName).Wait();
    }

    /// <summary>
    /// Persist current host system configuration
    /// </summary>
    /// <returns></returns>
    public async Task PersistCurrentHostSystemConfig()
    {
        await _systemList.PersistHostSystemConfig(_selectedSystemName);
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

        return _instrumentations.Stats
            .Union(_systemInstrumentations.Stats)
            .Union(_systemRunner.System.Instrumentations.Stats.Select(x => (Name: $"{_hostName}-{SystemTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.Renderer.Instrumentations.Stats.Select(x => (Name: $"{_hostName}-{RenderTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.AudioHandler.Instrumentations.Stats.Select(x => (Name: $"{_hostName}-{AudioTimeStatName}-{x.Name}", x.Stat)))
            .Union(_systemRunner.InputHandler.Instrumentations.Stats.Select(x => (Name: $"{_hostName}-{InputTimeStatName}-{x.Name}", x.Stat)))
            .ToList();
    }
}
