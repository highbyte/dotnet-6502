using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Rendering;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems;

public enum EmulatorState { Uninitialized, Running, Paused }

/// <summary>
/// A base class to be used as a common host application model for managing and running different emulators on a specific host platform.
/// The generic type parameters TInputHandlerContext, and TAudioHandlerContext dictates the types of input handling, and audio handling available on a specific platform.
/// 
/// The constructor must also provide a generic SystemList parameter (with the same generic context types) that provides the different emulators and their InputHandlers, and AudioHandlers base on the context types.
/// </summary>
/// <typeparam name="TInputHandlerContext"></typeparam>
/// <typeparam name="TAudioHandlerContext"></typeparam>
public class HostApp<TInputHandlerContext, TAudioHandlerContext> : IHostApp, IManualRenderingProvider
    where TInputHandlerContext : IInputHandlerContext
    where TAudioHandlerContext : IAudioHandlerContext
{
    // Injected via constructor
    private readonly ILogger _logger;
    private readonly SystemList<TInputHandlerContext, TAudioHandlerContext> _systemList;
    private readonly bool _useStatsNamePrefix;

    // Other variables
    private string _selectedSystemName;
    public string SelectedSystemName => _selectedSystemName;
    private ISystem? _selectedSystemTemporary; // A temporary storage of the selected system if asked for, and system has not been started yet.

    public HashSet<string> AvailableSystemNames
    {
        get
        {
            return _systemList.Systems;
        }
    }

    private string _selectedSystemConfigurationVariant;
    public string SelectedSystemConfigurationVariant => _selectedSystemConfigurationVariant;

    private List<string> _allSelectedSystemConfigurationVariants = new();
    public List<string> AllSelectedSystemConfigurationVariants
    {
        get
        {
            return _allSelectedSystemConfigurationVariants;
        }
        set
        {
            _allSelectedSystemConfigurationVariants = value;
            OnAfterAllSystemConfigurationVariantsChanged();
        }
    }

    private SystemRunner? _systemRunner = null;
    public SystemRunner? CurrentSystemRunner => _systemRunner;
    public ISystem? CurrentRunningSystem => _systemRunner?.System;
    public IScreen? CurrentSystemScreenInfo => _systemRunner != null ? _systemRunner?.System.Screen : _selectedSystemTemporary?.Screen;
    public EmulatorState EmulatorState
    {
        get
        {
            return _emulatorState;
        }
        private set
        {
            _emulatorState = value;
            OnAfterEmulatorStateChange();
        }
    }
    private IHostSystemConfig? _currentHostSystemConfig;

    private RenderTargetProvider? _renderTargetProvider;
    private RenderCoordinatorProvider? _renderCoordinatorProvider;
    private IRenderCoordinator? _renderCoordinator;
    private IRenderTarget? _currentRenderTarget;

    // Events
    public event EventHandler? SelectedSystemChanged;
    public event EventHandler? SelectedSystemVariantChanged;

    /// <summary>
    /// The current host system config.
    /// </summary>
    public IHostSystemConfig CurrentHostSystemConfig
    {
        get
        {
            if (_currentHostSystemConfig == null)
            {
                return null;
                // Trouble with Avalonia Browser binding because of timing of initialization would cause this exception to be thrown.
                // throw new DotNet6502Exception("Internal error. No system selected yet. Call SelectSystem() first.");
            }
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
    private string _statsPrefix => string.IsNullOrEmpty(_hostName) || !_useStatsNamePrefix ? string.Empty : $"{_hostName}-";
    private const string SystemTimeStatName = "SystemTime";
    private const string RenderStatName = "Render";
    private const string InputTimeStatName = "InputTime";
    private const string AudioTimeStatName = "AudioTime";
    private readonly Instrumentations _systemInstrumentations = new();
    private ElapsedMillisecondsTimedStatSystem? _systemTime;
    private ElapsedMillisecondsTimedStatSystem? _inputTime;
    private EmulatorState _emulatorState = EmulatorState.Uninitialized;

    //private ElapsedMillisecondsTimedStatSystem _audioTime;

    private readonly Instrumentations _instrumentations = new();
    private readonly PerSecondTimedStat _updateFps;

    public HostApp(
        string hostName,
        SystemList<TInputHandlerContext, TAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory,
        bool useStatsNamePrefix = true
        )
    {
        _hostName = hostName;
        _updateFps = _instrumentations.Add($"{_statsPrefix}OnUpdateFPS", new PerSecondTimedStat());

        _logger = loggerFactory.CreateLogger("HostApp");

        if (systemList.Systems.Count == 0)
            throw new DotNet6502Exception("No systems added to system list.");
        _systemList = systemList;
        _useStatsNamePrefix = useStatsNamePrefix;

        //Note: Because selecting a system (incl which variants it has) requires async call,
        //      call SelectSystem(string systemName) after HostApp is created to set the initial system.
        _selectedSystemName = "DEFAULT SYSTEM";
        _allSelectedSystemConfigurationVariants = new List<string> { "DEFAULT VARIANT" };
        _selectedSystemConfigurationVariant = _allSelectedSystemConfigurationVariants.First();
    }

    public void SetContexts(
        Func<TInputHandlerContext>? getInputHandlerContext = null,
        Func<TAudioHandlerContext>? getAudioHandlerContext = null
    )
    {
        if (_renderTargetProvider != null)
        {
            _systemList.SetContext(
                _renderTargetProvider, // New rendering pipeline
                getInputHandlerContext,
                getAudioHandlerContext);
        }
        else
        {
            // If no render config is set, still need to set the input/audio contexts
            _systemList.SetContext(
                new RenderTargetProvider(), // Empty render target provider for tests
                getInputHandlerContext,
                getAudioHandlerContext);
        }
    }

    public void InitInputHandlerContext() => _systemList.InitInputHandlerContext();
    public void InitAudioHandlerContext() => _systemList.InitAudioHandlerContext();

    public bool IsInputHandlerContextInitialized => _systemList.IsInputHandlerContextInitialized;
    public bool IsAudioHandlerContextInitialized => _systemList.IsAudioHandlerContextInitialized;

    /// <summary>
    /// Derived class must call this method once to configure rendering capabilities.
    /// Should not be called more than once.
    /// </summary>
    /// <param name="configureRenderTargetProvider"></param>
    /// <param name="createRenderLoop"></param>
    public void SetRenderConfig(
        Action<RenderTargetProvider> configureRenderTargetProvider,
        Func<IRenderLoop> createRenderLoop)
    {
        _renderTargetProvider = new RenderTargetProvider();
        configureRenderTargetProvider(_renderTargetProvider);

        var renderLoop = createRenderLoop();
        _renderCoordinatorProvider = new RenderCoordinatorProvider(renderLoop);
    }

    public List<Type> GetAvailableSystemRenderProviderTypes()
    {
        if (_renderTargetProvider == null)
            return new List<Type>();

        var systemRenderProviderTypes = CurrentHostSystemConfig.SystemConfig.GetSupportedRenderProviderTypes();
        var available = _renderTargetProvider.GetCompatibleConcreteRenderProviderTypes(systemRenderProviderTypes ?? new List<Type>());
        return available;
    }

    public List<(Type renderProviderType, Type renderTargetType)> GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations()
    {
        List<(Type rpt, Type rtt)> available = new();

        if (_renderTargetProvider == null)
            return available;

        var systemRenderProviderTypes = CurrentHostSystemConfig.SystemConfig.GetSupportedRenderProviderTypes();
        foreach (var rpt in systemRenderProviderTypes ?? new List<Type>())
        {
            var compatibleRtts = _renderTargetProvider.GetConcreteRenderTargetTypesForConcreteRenderProviderType(rpt);
            foreach (var rtt in compatibleRtts)
            {
                available.Add((rpt, rtt));
            }
        }
        return available;
    }

    public virtual void OnAfterEmulatorStateChange() { }

    public async Task SelectSystem(string systemName)
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            throw new DotNet6502Exception("Cannot change system while emulator is running.");
        if (!_systemList.Systems.Contains(systemName))
            throw new DotNet6502Exception($"System not found: {systemName}");

        bool systemChanged = systemName != _selectedSystemName;

        _selectedSystemName = systemName;
        CurrentHostSystemConfig = await _systemList.GetHostSystemConfig(_selectedSystemName);

        OnAfterSelectedSystemChanged();

        SelectedSystemChanged?.Invoke(this, EventArgs.Empty);

        // If system changed, make sure any state regarding the system variant is also in sync
        if (systemChanged)
        {
            AllSelectedSystemConfigurationVariants = await _systemList.GetSystemConfigurationVariants(systemName, CurrentHostSystemConfig);
            var selectedSystemConfigurationVariant = _allSelectedSystemConfigurationVariants.First();
            await SelectSystemConfigurationVariant(selectedSystemConfigurationVariant);
        }
    }

    public virtual void OnAfterAllSystemConfigurationVariantsChanged() { }

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

        OnAfterSelectedSystemVariantChanged();

        SelectedSystemVariantChanged?.Invoke(this, EventArgs.Empty);
    }

    public virtual void OnAfterSelectedSystemChanged() { }
    public virtual void OnAfterSelectedSystemVariantChanged() { }

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
            if (_selectedSystemTemporary == null || _systemList.HasConfigChanged(_selectedSystemName))
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

        if (EmulatorState == EmulatorState.Uninitialized)
        {
            InitRendererForSystem();
        }

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

        _systemRunner?.AudioHandler.StopPlaying();

        _renderCoordinator?.DisposeAsync();
        _renderCoordinator = null;
        _currentRenderTarget = null;

        // Cleanup systemrunner (which also cleanup renderer, inputhandler, and audiohandler)
        _systemRunner?.Cleanup();
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

        // Cleanup coordinators
        _renderCoordinator?.DisposeAsync();
        _renderCoordinator = null;
        _currentRenderTarget = null;
        _renderCoordinatorProvider = null;

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

    private void InitRendererForSystem()
    {
        // Skip rendering initialization if no render config has been provided (e.g., in unit tests)
        if (_renderTargetProvider == null || _renderCoordinatorProvider == null)
        {
            _renderCoordinator = null;
            _currentRenderTarget = null;
            return;
        }

        var renderTargetType = CurrentHostSystemConfig.SystemConfig.RenderTargetType;

        // Assume CurrentSystemRunner.System.RenderProvider is set to the selected system's render provider (one of possibly many in in system.RenderProviders).
        var renderTarget = _renderTargetProvider.CreateRenderTargetByRenderProviderType(CurrentSystemRunner!.System.RenderProvider.GetType(), renderTargetType);
        _currentRenderTarget = renderTarget;
        _renderCoordinator = _renderCoordinatorProvider.CreateRenderCoordinator(CurrentSystemRunner.System.RenderProvider, renderTarget, _instrumentations);
    }

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
                return null; //throw new DotNet6502Exception("Internal state error.");
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

    public async Task ApplySupportedRenderTargetToSystemConfigs()
    {
        foreach (var systemName in _systemList.Systems)
        {
            await _systemList.ApplySupportedRenderTargetToSystemConfig(systemName);
        }
    }

    private void InitInstrumentation(ISystem system)
    {
        _systemInstrumentations.Clear();
        _systemTime = _systemInstrumentations.Add($"{_statsPrefix}{SystemTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
        _inputTime = _systemInstrumentations.Add($"{_statsPrefix}{InputTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
        //_audioTime = InstrumentationBag.Add($"{_statsPrefix}{AudioTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
    }

    public List<(string name, IStat stat)> GetStats()
    {
        if (_systemRunner == null)
            return new List<(string name, IStat)>();

        var stats = _instrumentations.Stats
            // Overall stats
            .Union(_systemInstrumentations.Stats)
            // Sub-system stat: system
            .Union(_systemRunner.System.Instrumentations.Stats.Select(x => (Name: $"{_statsPrefix}{SystemTimeStatName}-{x.Name}", x.Stat)))
            // Sub-system stat: audio
            .Union(_systemRunner.AudioHandler.Instrumentations.Stats.Select(x => (Name: $"{_statsPrefix}{AudioTimeStatName}-{x.Name}", x.Stat)))
            // Sub-system stat: input
            .Union(_systemRunner.InputHandler.Instrumentations.Stats.Select(x => (Name: $"{_statsPrefix}{InputTimeStatName}-{x.Name}", x.Stat)))
            .ToList();

        // Sub-system stat: render
        if (_renderCoordinator != null)
            stats.AddRange(_renderCoordinator.Instrumentations.Stats.Select(x => (Name: $"{_statsPrefix}{RenderStatName}-{x.Name}", x.Stat)));

        return stats;
    }

    #region IManualRenderingProvider implementation

    /// <summary>
    /// Gets the current render coordinator for manual invalidation rendering scenarios.
    /// Only available when the emulator is running and using manual invalidation mode.
    /// </summary>
    public virtual IRenderCoordinator? GetRenderCoordinator()
    {
        // Only provide the render coordinator if we're in manual invalidation mode
        if (IsManualInvalidationMode)
        {
            return _renderCoordinator;
        }
        return null;
    }

    /// <summary>
    /// Gets a render target of the specified type if it's currently active and we're in manual invalidation mode.
    /// </summary>
    public virtual T? GetRenderTarget<T>() where T : class, IRenderTarget
    {
        if (IsManualInvalidationMode)
        {
            return _currentRenderTarget as T;
        }
        return null;
    }

    /// <summary>
    /// Indicates whether the host is using manual invalidation rendering mode.
    /// This is determined by checking if the render loop is in ManualInvalidation mode.
    /// </summary>
    public virtual bool IsManualInvalidationMode
    {
        get
        {
            if (_renderCoordinatorProvider?.RenderLoop != null)
            {
                return _renderCoordinatorProvider.RenderLoop.Mode == RenderTriggerMode.ManualInvalidation;
            }
            return false;
        }
    }

    #endregion
}
