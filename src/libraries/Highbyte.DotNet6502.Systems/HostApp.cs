using System.Collections.Concurrent;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Snapshots;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems;

public enum EmulatorState { Uninitialized, Running, Paused }

/// <summary>
/// A base class to be used as a common host application model for managing and running different emulators on a specific host platform.
///
/// Input and audio are no longer generic over host context types: the host input context is read
/// through the neutral <see cref="Input.IHostInputState"/>, and audio is driven by an
/// <see cref="Audio.IAudioCoordinator"/> wired up the same way the render pipeline is.
/// </summary>
public class HostApp : IHostApp, IManualRenderingProvider
{
    // Injected via constructor
    protected readonly ILogger _logger;
    private readonly SystemList _systemList;
    private readonly bool _useStatsNamePrefix;

    /// <summary>Host app / settings-schema identifier (e.g. "Avalonia", "Headless"). See <see cref="IHostApp.HostName"/>.</summary>
    public string HostName => _hostName;

    // Other variables
    private string _selectedSystemName;
    public string SelectedSystemName => _selectedSystemName;

    private ISystem? _selectedSystemTemporaryBacking; // A temporary storage of the selected system if asked for, and system has not been started yet.
    private ISystem? _selectedSystemTemporary
    {
        get => _selectedSystemTemporaryBacking;
        set
        {
            _selectedSystemTemporaryBacking = value;
            // Remember whether the selected system supports snapshots. Updated only when a system
            // instance is established; deliberately NOT reset when the instance is cleared (e.g. by
            // Stop()), so the snapshot capability of the still-selected system survives a stop —
            // see SelectedSystemSupportsSnapshots.
            if (value != null)
                _selectedSystemSupportsSnapshots = value is ISystemSnapshotProvider;
        }
    }

    // Cached snapshot capability of the currently selected system (by name). Persists across Stop(),
    // which clears _selectedSystemTemporary but leaves the system selected.
    private bool _selectedSystemSupportsSnapshots;

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
            SetAllSelectedSystemConfigurationVariants(value);
        }
    }

    private SystemRunner? _systemRunner = null;
    public SystemRunner? CurrentSystemRunner => _systemRunner;
    public ISystem? CurrentRunningSystem => _systemRunner?.System;
    public IScreen? CurrentSystemScreenInfo => _systemRunner != null
        ? _systemRunner.System.Screen
        : _selectedSystemTemporary?.Screen
            ?? (_currentHostSystemConfig != null
                ? _systemList.GetScreenInfo(_selectedSystemName, _selectedSystemConfigurationVariant, _currentHostSystemConfig)
                : null);
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

    private AudioTargetProvider? _audioTargetProvider;
    private AudioCoordinatorProvider? _audioCoordinatorProvider;
    private IAudioCoordinator? _audioCoordinator;
    private IAudioTarget? _currentAudioTarget;

    // The host input context, which is also the neutral input source (IHostInputState) bound to
    // the running system's IInputConsumer. Mirrors how render/audio targets are registered host-side.
    private Func<IInputHandlerContext>? _getInputHandlerContext;

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
                return null!;
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
    private const string AudioStatName = "Audio";
    private const string ScriptTimeStatName = "ScriptTime";
    private readonly Instrumentations _systemInstrumentations = new();
    private ElapsedMillisecondsTimedStatSystem? _systemTime;
    private ElapsedMillisecondsTimedStatSystem? _inputTime;
    private ElapsedMillisecondsTimedStatSystem? _scriptTime;
    private EmulatorState _emulatorState = EmulatorState.Uninitialized;

    //private ElapsedMillisecondsTimedStatSystem _audioTime;

    private readonly Instrumentations _instrumentations = new();
    private readonly PerSecondTimedStat _updateFps;

    // Remote control action queue (drained at the frame boundary via DrainPendingRemoteActions)
    private readonly ConcurrentQueue<Action> _pendingRemoteActions = new();

    // Scripting
    private IScriptingEngine _scriptingEngine = new NoScriptingEngine();
    private IScriptingTickTimer? _scriptingTickTimer;
    private string? _userSettingsFilePath;
    private ScriptingConfig? _scriptingConfig;

    /// <summary>
    /// Interval between scripting coroutine resume ticks. Roughly 60 Hz, independent of the emulator frame rate.
    /// </summary>
    protected const double ScriptingTickIntervalMs = 16.0;

    /// <summary>Exposes the scripting engine to subclasses (e.g., for Scripts tab UI).</summary>
    protected IScriptingEngine ScriptingEngine => _scriptingEngine;

    public HostApp(
        string hostName,
        SystemList systemList,
        ILoggerFactory loggerFactory,
        bool useStatsNamePrefix = true
        )
    {
        _hostName = hostName;
        _updateFps = _instrumentations.Add($"{_statsPrefix}OnUpdateFPS", new PerSecondTimedStat());
        // (HostName exposes _hostName for snapshot host-config tagging; see HostName property.)

        _logger = loggerFactory.CreateLogger("HostApp");

        // An empty system list is a valid (degraded) state: the host app is still created so its
        // UI can be reached and a "no systems available" error can be shown to the user. Selecting
        // or running a system is what fails later — host apps handle that by not attempting it.
        if (systemList.Systems.Count == 0)
            _logger.LogWarning(
                "HostApp created with no systems in the system list. No system can be selected or run.");
        _systemList = systemList;
        _useStatsNamePrefix = useStatsNamePrefix;

        //Note: Because selecting a system (incl which variants it has) requires async call,
        //      call SelectSystem(string systemName) after HostApp is created to set the initial system.
        _selectedSystemName = "DEFAULT SYSTEM";
        _allSelectedSystemConfigurationVariants = new List<string> { "DEFAULT VARIANT" };
        _selectedSystemConfigurationVariant = _allSelectedSystemConfigurationVariants.First();
    }

    // --- Scripting ---

    /// <summary>
    /// Sets the Lua scripting engine. Call before starting the emulator.
    /// Pass a <see cref="NoScriptingEngine"/> (or call with no arg) to disable scripting.
    /// </summary>
    public void SetScriptingEngine(IScriptingEngine engine)
    {
        _scriptingEngine = engine;
        _scriptingEngine.SetHostApp(this);
        _scriptingEngine.LoadScripts();
        if (_scriptingEngine.IsEnabled)
        {
            _scriptingTickTimer = CreateScriptingTickTimer(ScriptingTickIntervalMs);
            if (_scriptingTickTimer != null)
            {
                _scriptingTickTimer.Elapsed += ScriptingTickTimerElapsed;
                _scriptingTickTimer.Start();
            }
            else
            {
                _logger.LogWarning("Scripting engine is enabled but host did not supply a scripting tick timer; emu.yield() coroutines will not resume.");
            }
            OnScriptingEngineSet();
        }
    }

    public void SetStoragePathsContext(string userSettingsFilePath, ScriptingConfig? scriptingConfig = null)
    {
        _userSettingsFilePath = userSettingsFilePath;
        _scriptingConfig = scriptingConfig;
    }

    public Task<StoragePathsInfo> GetStoragePathsInfoAsync()
    {
        var userSettingsFilePath = _userSettingsFilePath ?? AppStoragePaths.GetUserSettingsFilePath(HostName);
        return StoragePathsInfoFactory.CreateAsync(HostName, userSettingsFilePath, _systemList, _scriptingConfig);
    }

    /// <summary>
    /// Override to supply the platform-specific periodic timer driving emu.yield() coroutine resumption.
    /// Return null if this host does not support scripting (default).
    /// </summary>
    protected virtual IScriptingTickTimer? CreateScriptingTickTimer(double intervalMs) => null;

    /// <summary>
    /// Optional post-setup hook invoked after the scripting engine is wired and the tick timer started.
    /// Override to perform an initial drain of pending script actions (e.g. emu.start() from top-level script).
    /// </summary>
    protected virtual void OnScriptingEngineSet() { }

    private void StopScriptingTimer()
    {
        if (_scriptingTickTimer != null)
        {
            _scriptingTickTimer.Elapsed -= ScriptingTickTimerElapsed;
            _scriptingTickTimer.Stop();
            _scriptingTickTimer.Dispose();
            _scriptingTickTimer = null;
        }
    }

    private async void ScriptingTickTimerElapsed(object? sender, EventArgs e)
    {
        try
        {
            if (_scriptingEngine.IsEnabled)
                _scriptingEngine.ResumeCoroutines();
            await _scriptingEngine.DrainPendingActionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in scripting tick timer.");
        }
    }

    /// <summary>
    /// Drains deferred script actions (e.g. emu.start()). Call after RunEmulatorOneFrame() from the
    /// host's emulator update loop, so script-initiated emulator state changes take effect at the frame boundary.
    /// </summary>
    protected async Task DrainPendingScriptActionsAsync()
    {
        await _scriptingEngine.DrainPendingActionsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Enqueues an action to be executed at the next frame boundary.
    /// Thread-safe; called from the remote control session thread.
    /// </summary>
    public void EnqueueRemoteAction(Action action) => _pendingRemoteActions.Enqueue(action);

    /// <summary>
    /// Drains all pending remote actions at the frame boundary.
    /// </summary>
    protected void DrainPendingRemoteActions()
    {
        while (_pendingRemoteActions.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Remote action threw exception"); }
        }
    }

    // --- End Scripting ---

    public void SetContexts(
        Func<IInputHandlerContext>? getInputHandlerContext = null
    )
    {
        _getInputHandlerContext = getInputHandlerContext;

        // Use the configured render target provider, or an empty one for tests where no render config is set.
        _systemList.SetContext(
            _renderTargetProvider ?? new RenderTargetProvider(),
            getInputHandlerContext);
    }

    public void InitInputHandlerContext() => _systemList.InitInputHandlerContext();

    public bool IsInputHandlerContextInitialized => _systemList.IsInputHandlerContextInitialized;

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

    /// <summary>
    /// Derived class may call this once to configure audio capabilities (mirrors <see cref="SetRenderConfig"/>).
    /// Hosts that support no audio simply do not call it.
    /// </summary>
    /// <param name="configureAudioTargetProvider">
    /// Callback to register the host's <see cref="IAudioTarget"/> factories (which close over the
    /// host's audio context).
    /// </param>
    public void SetAudioConfig(
        Action<AudioTargetProvider> configureAudioTargetProvider)
    {
        _audioTargetProvider = new AudioTargetProvider();
        configureAudioTargetProvider(_audioTargetProvider);

        _audioCoordinatorProvider = new AudioCoordinatorProvider();
    }

    /// <summary>Pauses just the audio output (e.g. when an external debugger halts the system).</summary>
    protected void PauseAudio() => _audioCoordinator?.PausePlaying();

    /// <summary>Resumes just the audio output (e.g. when an external debugger continues the system).</summary>
    protected void ResumeAudio() => _audioCoordinator?.StartPlaying();

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

    /// <summary>
    /// Returns every concrete (audioProviderType, audioTargetType) combination the host can build
    /// for the current system. Audio counterpart of
    /// <see cref="GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations"/>.
    /// </summary>
    public List<(Type audioProviderType, Type audioTargetType)> GetAvailableSystemAudioProviderTypesAndAudioTargetTypeCombinations()
    {
        List<(Type apt, Type att)> available = new();

        if (_audioTargetProvider == null)
            return available;

        var systemAudioProviderTypes = CurrentHostSystemConfig.SystemConfig.GetSupportedAudioProviderTypes();
        foreach (var apt in systemAudioProviderTypes ?? new List<Type>())
        {
            var compatibleAtts = _audioTargetProvider.GetConcreteAudioTargetTypesForConcreteAudioProviderType(apt);
            foreach (var att in compatibleAtts)
            {
                available.Add((apt, att));
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

        // If system changed, make sure any state regarding the system variant is also in sync
        if (systemChanged)
        {
            SetAllSelectedSystemConfigurationVariants(
                await _systemList.GetSystemConfigurationVariants(systemName, CurrentHostSystemConfig),
                notify: false);
            if (_allSelectedSystemConfigurationVariants.Count > 0)
            {
                _selectedSystemConfigurationVariant = _allSelectedSystemConfigurationVariants.First();
                await UpdateSelectedSystemTemporary();
            }
            else
            {
                // A system with no configuration variants cannot be built or run. Keep it
                // selected so the UI can still show it, but leave it in a non-runnable state
                // (Start() rejects it) instead of crashing here on First(). This is a system-
                // plugin defect: ISystemConfigurer.GetConfigurationVariants must return at least
                // one variant (use a single "DEFAULT" if the system has no real variants).
                _logger.LogError(
                    "System '{System}' has no configuration variants — it cannot be started. " +
                    "ISystemConfigurer.GetConfigurationVariants must return at least one variant.",
                    systemName);
                _selectedSystemConfigurationVariant = string.Empty;
                _selectedSystemTemporary = null;
            }
        }

        OnAfterAllSystemConfigurationVariantsChanged();
        OnAfterSelectedSystemVariantChanged();
        OnAfterSelectedSystemChanged();

        SelectedSystemVariantChanged?.Invoke(this, EventArgs.Empty);
        SelectedSystemChanged?.Invoke(this, EventArgs.Empty);
        _scriptingEngine.InvokeEvent("on_variant_selected", SelectedSystemConfigurationVariant);
        _scriptingEngine.InvokeEvent("on_system_selected", SelectedSystemName);
    }

    public virtual void OnAfterAllSystemConfigurationVariantsChanged() { }

    private void SetAllSelectedSystemConfigurationVariants(List<string> variants, bool notify = true)
    {
        _allSelectedSystemConfigurationVariants = variants;
        if (notify)
            OnAfterAllSystemConfigurationVariantsChanged();
    }

    public async Task SelectSystemConfigurationVariant(string configurationVariant)
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            throw new DotNet6502Exception("Cannot change system while emulator is running.");
        var configVariants = await _systemList.GetSystemConfigurationVariants(_selectedSystemName, CurrentHostSystemConfig);
        if (!configVariants.Contains(configurationVariant))
            throw new DotNet6502Exception($"System configuration variant not found: {configurationVariant}");

        _selectedSystemConfigurationVariant = configurationVariant;
        await UpdateSelectedSystemTemporary();

        OnAfterSelectedSystemVariantChanged();

        SelectedSystemVariantChanged?.Invoke(this, EventArgs.Empty);
        _scriptingEngine.InvokeEvent("on_variant_selected", SelectedSystemConfigurationVariant);
    }

    private async Task UpdateSelectedSystemTemporary()
    {
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

    public virtual void OnAfterSelectedSystemChanged() { }
    public virtual void OnAfterSelectedSystemVariantChanged() { }

    public virtual bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        return true;
    }

    public Task<(bool IsValid, List<string> Errors)> IsCurrentSystemConfigValid() =>
        _systemList.IsValidConfigWithDetails(_selectedSystemName);

    public Task<(bool IsValid, List<string> Errors)> IsSystemConfigValid(string systemName) =>
        _systemList.IsValidConfigWithDetails(systemName);

    public async Task Start()
    {
        if (EmulatorState == EmulatorState.Running)
            throw new DotNet6502Exception("Cannot start emulator if emulator is running.");

        // A system with no configuration variants cannot be built (see SelectSystem). Fail with a
        // clear message rather than passing an empty variant name down to BuildSystem.
        if (_allSelectedSystemConfigurationVariants.Count == 0)
            throw new DotNet6502Exception(
                $"Cannot start system '{_selectedSystemName}': it has no configuration variants. " +
                "This is a system-plugin defect — ISystemConfigurer.GetConfigurationVariants must " +
                "return at least one variant.");

        var (isValid, validationErrors) = await _systemList.IsValidConfigWithDetails(_selectedSystemName);
        if (!isValid)
        {
            var details = validationErrors.Count > 0
                ? string.Join("; ", validationErrors)
                : "no details available";
            throw new DotNet6502Exception($"Cannot start emulator, system config is invalid: {details}");
        }

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
            InitAudioForSystem();
            InitInputForSystem();
        }

        _audioCoordinator?.StartPlaying();

        OnAfterStart(emulatorStateBeforeStart);
        _scriptingEngine.OnSystemStarted(CurrentRunningSystem!);
        _scriptingEngine.InvokeEvent("on_started");

        EmulatorState = EmulatorState.Running;
        _logger.LogInformation($"System started: {_selectedSystemName} Variant: {_selectedSystemConfigurationVariant}");

    }
    public virtual void OnAfterStart(EmulatorState emulatorStateBeforeStart) { }

    public void Pause()
    {
        if (EmulatorState == EmulatorState.Paused || EmulatorState == EmulatorState.Uninitialized)
            return;

        _audioCoordinator?.PausePlaying();

        OnAfterPause();
        _scriptingEngine.InvokeEvent("on_paused");

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

        _audioCoordinator?.StopPlaying();

        _renderCoordinator?.DisposeAsync();
        _renderCoordinator = null;
        _currentRenderTarget = null;

        _audioCoordinator?.DisposeAsync();
        _audioCoordinator = null;
        _currentAudioTarget = null;

        // Cleanup systemrunner (which also cleans up the input handler)
        _systemRunner?.Cleanup();
        _systemRunner = default!;

        EmulatorState = EmulatorState.Uninitialized;
        _selectedSystemTemporary = null; // Clear the temporary system, as it is no longer valid.

        OnAfterStop();
        _scriptingEngine.InvokeEvent("on_stopped");

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

    /// <summary>
    /// True if the currently running (or selected-but-not-started) system instance supports
    /// snapshots. False after <see cref="Stop"/> (no system instance exists to capture). Use this to
    /// gate <b>saving</b>, which needs a live system to read state from.
    /// </summary>
    public bool CanSnapshotCurrentSystem
        => (CurrentRunningSystem ?? _selectedSystemTemporary) is ISystemSnapshotProvider;

    /// <summary>
    /// True if the currently <b>selected</b> system (by name) supports snapshots, independent of
    /// whether it is started. Unlike <see cref="CanSnapshotCurrentSystem"/> this survives
    /// <see cref="Stop"/>, so it stays consistent between a freshly launched app and a stopped one.
    /// Use this to gate <b>loading</b>, which rebuilds the machine and so does not need a live system.
    /// </summary>
    public bool SelectedSystemSupportsSnapshots
        => (CurrentRunningSystem ?? _selectedSystemTemporary) is ISystemSnapshotProvider || _selectedSystemSupportsSnapshots;

    /// <summary>
    /// Captures a snapshot of the current system to <paramref name="output"/>. Pauses the emulator
    /// for the (read-only) capture and resumes it afterwards if it had been running. Throws
    /// <see cref="SnapshotException"/> if no snapshot-capable system is available.
    /// </summary>
    public async Task SaveSnapshotAsync(Stream output, SnapshotSaveOptions? options = null, bool includeConfig = false)
    {
        var system = CurrentRunningSystem ?? _selectedSystemTemporary
            ?? throw new SnapshotException("No system is selected or running to snapshot.");
        if (system is not ISystemSnapshotProvider)
            throw new SnapshotException($"System '{system.Name}' does not support snapshots yet.");

        options ??= new SnapshotSaveOptions { ConfigurationVariant = _selectedSystemConfigurationVariant };

        // Optionally capture the current runtime settings ("config"). The host-specific block is
        // produced by the concrete host app; the portable system-config block is captured separately
        // (see CaptureConfigForSnapshotAsync). Reads current runtime values, not appsettings defaults.
        if (includeConfig)
            options.Config = await CaptureConfigForSnapshotAsync();

        bool wasRunning = EmulatorState == EmulatorState.Running;
        if (wasRunning)
            Pause();
        try
        {
            new SnapshotService().Save(system, output, options);
            _logger.LogInformation("Saved snapshot of system '{System}' variant '{Variant}'.",
                system.Name, _selectedSystemConfigurationVariant);
        }
        finally
        {
            if (wasRunning)
                await Start();
        }
    }

    /// <summary>
    /// Restores a snapshot read from <paramref name="input"/>. Stops any running system, rebuilds a
    /// fresh system of the snapshot's machine + variant, restores module state into it, then starts
    /// it left paused. Returns the restore result (including any non-fatal warnings).
    /// </summary>
    public async Task<SnapshotRestoreResult> LoadSnapshotAsync(Stream input, bool applyConfig = false)
    {
        var manifest = SnapshotService.PeekManifest(input);
        input.Position = 0;

        var targetSystemName = manifest.Machine.SystemName;
        if (!_systemList.Systems.Contains(targetSystemName))
            throw new SnapshotException(
                $"Snapshot is for system '{targetSystemName}', which is not available in this host.");

        if (EmulatorState != EmulatorState.Uninitialized)
            Stop();

        await SelectSystem(targetSystemName);

        var variant = manifest.Machine.ConfigurationVariant;
        if (!string.IsNullOrEmpty(variant)
            && _allSelectedSystemConfigurationVariants.Contains(variant)
            && variant != _selectedSystemConfigurationVariant)
        {
            await SelectSystemConfigurationVariant(variant);
        }

        // Apply the embedded settings ("config") if the user opted in — BEFORE building/starting, so
        // both the rebuilt machine and its initialized host services reflect them. This matters
        // because several settings are read at build/init time, not as live toggles: e.g. the C64
        // keyboard-joystick is read from config when the machine is built, and audio (enabled + volume)
        // is wired up when the audio pipeline is initialized during Start.
        if (applyConfig)
        {
            input.Position = 0;
            var peekedConfig = SnapshotService.PeekConfig(input);
            input.Position = 0;
            if (!string.IsNullOrEmpty(peekedConfig?.SystemConfigJson))
                ApplySystemConfigBeforeRebuild(peekedConfig!.SystemConfigJson!);
            if (!string.IsNullOrEmpty(peekedConfig?.HostJson))
                await ApplyHostSnapshotConfigAsync(peekedConfig!.HostJson!);
            // Notify config-bound UI once the final values are in place, so controls (e.g. the audio
            // checkbox) reflect the restored settings rather than the pre-load values.
            if (peekedConfig is { IsEmpty: false })
                OnAfterHostSystemConfigUpdated();
        }

        // Build a fresh system of the target machine + variant and restore the snapshot into it.
        // BuildSystem clears the config-dirty flag, so the subsequent Start() reuses this exact
        // instance instead of rebuilding it (and discarding the restored state).
        var freshSystem = await _systemList.BuildSystem(_selectedSystemName, _selectedSystemConfigurationVariant);
        var result = new SnapshotService().Restore(freshSystem, input);

        _selectedSystemTemporary = freshSystem;
        await Start();
        Pause();

        foreach (var warning in result.Warnings)
            _logger.LogWarning("Snapshot restore warning: {Warning}", warning);
        _logger.LogInformation("Restored snapshot into system '{System}' variant '{Variant}' (paused).",
            _selectedSystemName, _selectedSystemConfigurationVariant);

        // (Both config blocks were applied before the rebuild/start above, so the running machine and
        // its host services already reflect them.)

        return result;
    }

    // --- Snapshot runtime-settings ("config") capture/apply orchestration ---

    /// <summary>
    /// Gathers the runtime-settings blocks to embed in a snapshot: the portable system-config block
    /// (from the global <see cref="ISystemConfig"/> if it opts in via <see cref="ISnapshotableConfig"/>)
    /// and the host-specific block (from the concrete host app). Returns null if neither produced
    /// anything. Reads current runtime values, not appsettings defaults.
    /// </summary>
    private async Task<SnapshotConfigContent?> CaptureConfigForSnapshotAsync()
    {
        var systemConfigJson = (CurrentHostSystemConfig?.SystemConfig as ISnapshotableConfig)?.ExportSnapshotSettings();
        var hostJson = await CaptureHostSnapshotConfigAsync();
        var content = new SnapshotConfigContent { SystemConfigJson = systemConfigJson, HostJson = hostJson };
        return content.IsEmpty ? null : content;
    }

    /// <summary>
    /// Applies the portable system-config block to the current system config <b>before</b> the machine
    /// is rebuilt, so the rebuilt machine reflects it (no live-machine poke). Called from
    /// <see cref="LoadSnapshotAsync"/> after the target system/variant is selected but before build.
    /// </summary>
    private void ApplySystemConfigBeforeRebuild(string systemConfigJson)
    {
        if (CurrentHostSystemConfig?.SystemConfig is not ISnapshotableConfig snapshotable)
            return;
        snapshotable.ApplySnapshotSettings(systemConfigJson);
        // Propagate the mutated config so BuildSystem picks it up.
        UpdateHostSystemConfig(CurrentHostSystemConfig);
    }

    /// <summary>
    /// Capture the host-specific block (general host-app settings, e.g. audio/scale, plus the
    /// per-backend host system config). Override in a concrete host app; base returns null (no host
    /// settings, e.g. headless). The block should carry the originating <see cref="HostName"/> so a
    /// loading host can decide whether it applies.
    /// </summary>
    protected virtual Task<string?> CaptureHostSnapshotConfigAsync() => Task.FromResult<string?>(null);

    /// <summary>
    /// Apply a host-specific block previously produced by <see cref="CaptureHostSnapshotConfigAsync"/>.
    /// The override should ignore a block whose host does not match this one (logging an info/warning)
    /// and reproduce the full effect of the corresponding UI actions. Base is a no-op.
    /// </summary>
    protected virtual Task ApplyHostSnapshotConfigAsync(string payload) => Task.CompletedTask;

    public void Close()
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            Stop();

        // Cleanup coordinators
        _renderCoordinator?.DisposeAsync();
        _renderCoordinator = null;
        _currentRenderTarget = null;
        _renderCoordinatorProvider = null;

        _audioCoordinator?.DisposeAsync();
        _audioCoordinator = null;
        _currentAudioTarget = null;
        _audioCoordinatorProvider = null;

        _logger.LogInformation($"Emulator closed");

        StopScriptingTimer();
        OnAfterClose();
    }
    public virtual void OnAfterClose() { }

    public virtual void QuitApplication()
    {
        _logger.LogInformation("QuitApplication requested — closing host (no platform-specific shutdown override)");
        Close();
    }

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

        // Frame is definitely going to run

        // Handle remote control action
        CurrentRunningSystem?.InputInjector?.BeginFrame();
        DrainPendingRemoteActions();

        // Invoke scripting before-frame hook
        _scriptTime!.Start();
        _scriptingEngine.InvokeBeforeFrame();
        _scriptTime!.Stop(cont: true); // accumulate; don't record until after-frame scripting completes

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

        _scriptTime!.Start(cont: true); // continue accumulating from before-frame measurement
        _scriptingEngine.InvokeAfterFrame();
        _scriptTime!.Stop(); // record total script time for this frame
    }

    public virtual void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult) { }

    /// <summary>
    /// Deterministically steps the loaded system by <paramref name="frameCount"/> frames and flushes
    /// the render coordinator so the host's render target (e.g. the desktop screenshot bitmap)
    /// reflects the final frame. Bypasses the real-time run loop — it is intended for automation
    /// (load a snapshot paused, step a known number of frames, then screenshot).
    /// Requires the emulator to be stopped/paused: stepping while it is Running is rejected, because
    /// the real-time run loop is concurrently advancing the same system, which would make the step
    /// non-deterministic.
    /// </summary>
    public async Task StepEmulatorFramesAsync(int frameCount)
    {
        if (frameCount < 1)
            throw new DotNet6502Exception($"frameCount must be >= 1 (was {frameCount}).");
        if (_systemRunner == null)
            throw new DotNet6502Exception("No system is loaded to step (load/start a system or restore a snapshot first).");
        if (EmulatorState == EmulatorState.Running)
            throw new DotNet6502Exception("Cannot step frames while the emulator is running; deterministic stepping requires a stopped/paused emulator. Pause the emulator (or load a snapshot) first.");

        for (int i = 0; i < frameCount; i++)
            _systemRunner.RunEmulatorOneFrame();

        // Draw the resulting frame to the render target so a subsequent screenshot reflects it.
        // Null on hosts without a renderer (e.g. headless) — stepping still advances emulation.
        if (_renderCoordinator != null)
            await _renderCoordinator.FlushIfDirtyAsync();
    }

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
        var renderProvider = CurrentSystemRunner?.System.RenderProvider;
        if (renderProvider == null)
        {
            _renderCoordinator = null;
            _currentRenderTarget = null;
            return;
        }

        // Assume CurrentSystemRunner.System.RenderProvider is set to the selected system's render provider (one of possibly many in in system.RenderProviders).
        var renderTarget = _renderTargetProvider.CreateRenderTargetByRenderProviderType(renderProvider.GetType(), renderTargetType);
        _currentRenderTarget = renderTarget;
        _renderCoordinator = _renderCoordinatorProvider.CreateRenderCoordinator(renderProvider, renderTarget, _instrumentations);
    }

    private void InitAudioForSystem()
    {
        // Skip if no audio config has been provided (host has no audio support, or unit tests).
        if (_audioTargetProvider == null || _audioCoordinatorProvider == null)
        {
            _audioCoordinator = null;
            _currentAudioTarget = null;
            return;
        }

        // The system exposes its audio provider (null if the system produces no audio,
        // e.g. the C64 with audio disabled).
        var audioProvider = CurrentSystemRunner?.System.AudioProvider;
        if (audioProvider == null)
        {
            _audioCoordinator = null;
            _currentAudioTarget = null;
            return;
        }

        var audioTargetType = CurrentHostSystemConfig.SystemConfig.AudioTargetType;
        var audioTarget = _audioTargetProvider.CreateAudioTargetByAudioProviderType(audioProvider.GetType(), audioTargetType);
        _currentAudioTarget = audioTarget;
        _audioCoordinator = _audioCoordinatorProvider.CreateAudioCoordinator(audioProvider, audioTarget);
        _audioCoordinator.Init();
    }

    /// <summary>
    /// Binds the host input state to the running system's input consumer (input counterpart of
    /// <see cref="InitRendererForSystem"/> / <see cref="InitAudioForSystem"/>). The system exposes
    /// its <see cref="ISystem.InputConsumer"/>; the host supplies the neutral
    /// <see cref="IHostInputState"/> via the input context registered in <see cref="SetContexts"/>.
    /// </summary>
    private void InitInputForSystem()
    {
        // Null if the system consumes no input (e.g. the Headless host wires no input consumer).
        var inputConsumer = CurrentSystemRunner?.System.InputConsumer;
        if (inputConsumer == null)
            return;

        // The host input context implements IHostInputState. Skip if no context was provided.
        if (_getInputHandlerContext?.Invoke() is not IHostInputState hostInputState)
            return;

        inputConsumer.Init(hostInputState);
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

    public async Task<ISystem?> GetSelectedSystem()
    {
        if (EmulatorState == EmulatorState.Uninitialized)
        {
            // If we haven't started started the selected system yet, return a temporary instance of the system (set in SelectSystem method).
            if (_selectedSystemTemporary == null)
                return null;
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

        // Rebuild the pre-created system instance to reflect the updated config (e.g. after ROMs have
        // been uploaded for the first time), so that GetSelectedSystem() returns a valid system and
        // UpdateCanvasSize() can correctly size the emulator canvas.
        // Skip this when the emulator is already running -- the config change will be picked up at
        // the next start via HasConfigChanged in Start().
        if (EmulatorState == EmulatorState.Uninitialized)
            SelectSystemConfigurationVariant(_selectedSystemConfigurationVariant).Wait();

        OnAfterHostSystemConfigUpdated();
    }

    /// <summary>
    /// Called after UpdateHostSystemConfig has stored the new config.
    /// Override in subclasses to notify the UI that CurrentHostSystemConfig has changed.
    /// </summary>
    public virtual void OnAfterHostSystemConfigUpdated() { }

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
        _scriptTime = _systemInstrumentations.Add($"{_statsPrefix}{ScriptTimeStatName}", new ElapsedMillisecondsTimedStatSystem(system));
        //_audioTime = InstrumentationBag.Add($"{_statsPrefix}{AudioStatName}", new ElapsedMillisecondsTimedStatSystem(system));
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
            .ToList();

        // Sub-system stat: input
        if (_systemRunner.System.InputConsumer != null)
            stats.AddRange(_systemRunner.System.InputConsumer.Instrumentations.Stats.Select(x => (Name: $"{_statsPrefix}{InputTimeStatName}-{x.Name}", x.Stat)));

        // Sub-system stat: render
        if (_renderCoordinator != null)
            stats.AddRange(_renderCoordinator.Instrumentations.Stats.Select(x => (Name: $"{_statsPrefix}{RenderStatName}-{x.Name}", x.Stat)));
        if (_currentRenderTarget is IInstrumentationSource renderTargetInstrumentation)
            stats.AddRange(renderTargetInstrumentation.Instrumentations.Stats.Select(x => (Name: $"{_statsPrefix}{RenderStatName}-Target-{x.Name}", x.Stat)));

        // Sub-system stat: audio
        if (_audioCoordinator != null)
            stats.AddRange(_audioCoordinator.Instrumentations.Stats.Select(x => (Name: $"{_statsPrefix}{AudioStatName}-{x.Name}", x.Stat)));
        if (_currentAudioTarget is IInstrumentationSource audioTargetInstrumentation)
            stats.AddRange(audioTargetInstrumentation.Instrumentations.Stats.Select(x => (Name: $"{_statsPrefix}Audio-Target-{x.Name}", x.Stat)));

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
