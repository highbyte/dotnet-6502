using Blazored.LocalStorage;
using Blazored.Modal;
using Blazored.Modal.Services;
using Highbyte.DotNet6502.App.WASM.Emulator;
using Highbyte.DotNet6502.App.WASM.Emulator.Skia;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.WebUtilities;
using Toolbelt.Blazor.Gamepad;
using Highbyte.DotNet6502.Systems.Logging.Console;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;

namespace Highbyte.DotNet6502.App.WASM.Pages;

public partial class Index : IWasmHostView
{
    //private string Version => typeof(Program).Assembly.GetName().Version!.ToString();
    private string Version => Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    /// <summary>
    /// Flag to indicate if the component has been initialized (set after OnInitializedAsync has been run) 
    /// to allow sub components to know if they can render code dependent on variables in this component.
    /// </summary>
    public bool Initialized { get; private set; } = false;

    /// <summary>
    /// Set when the app cannot start (e.g. no emulator systems available). When non-null the page
    /// shows a fatal error message instead of the emulator UI. A browser tab cannot quit itself,
    /// so the message offers no action and cannot be dismissed.
    /// </summary>
    public string? StartupError { get; private set; }

    private AudioContextSync _audioContext = default!;
    private SKCanvas _canvas = default!;
    private GRContext _grContext = default!;

    public EmulatorState CurrentEmulatorState => _wasmHost.EmulatorState;

    private bool IsSelectedSystemConfigOk => string.IsNullOrEmpty(_selectedSystemConfigValidationMessage);
    private string _selectedSystemConfigValidationMessage = "";

    private bool _isAudioSupported;
    private bool _isAudioEnabled;
    private bool AudioEnabledToggleDisabled => !_isAudioSupported || CurrentEmulatorState == EmulatorState.Running || CurrentEmulatorState == EmulatorState.Paused;

    private async Task RefreshAudioUiState()
    {
        if (_wasmHost == null)
        {
            _isAudioSupported = false;
            _isAudioEnabled = false;
            return;
        }

        _isAudioSupported = await _wasmHost.IsAudioSupported();
        _isAudioEnabled = _isAudioSupported && await _wasmHost.IsAudioEnabled();
    }

    private async Task SetAudioEnabled(bool enabled)
    {
        if (_wasmHost == null)
            return;
        await _wasmHost.SetAudioEnabled(enabled);
        _isAudioEnabled = enabled;
        await this.StateHasChangedCustom();
    }

    private float _masterVolumePercent = 10.0f;
    private float MasterVolumePercent
    {
        get
        {
            return _masterVolumePercent;
        }
        set
        {
            _masterVolumePercent = value;
            _wasmHost!.SetVolumePercent(_masterVolumePercent);
        }
    }

    private double Scale
    {
        get
        {
            return _emulatorConfig.CurrentDrawScale;
        }
        set
        {
            _emulatorConfig.CurrentDrawScale = value;
            WasmTaskHelper.Observe(UpdateCanvasSize(), nameof(UpdateCanvasSize));
        }
    }

    private ElementReference _monitorInputRef = default!;
    private EmulatorConfig _emulatorConfig = default!;

    private SkiaWASMHostApp _wasmHost = default!;
    public SkiaWASMHostApp WasmHost => _wasmHost;
    public string SelectedSystemName => _wasmHost.SelectedSystemName;
    public string SelectedSystemConfigurationVariant => _wasmHost.SelectedSystemConfigurationVariant;
    public ISystem? CurrentRunningSystem => _wasmHost.CurrentRunningSystem;
    public SystemRunner? CurrentSystemRunner => _wasmHost.CurrentSystemRunner;
    public IHostSystemConfig CurrentHostSystemConfig => _wasmHost.CurrentHostSystemConfig;

    private string _statsString = "Instrumentations: calculating...";
    private string _debugString = "";

    private string _windowWidthStyle = "0px";
    private string _windowHeightStyle = "0px";

    private bool _debugVisible = false;
    private bool _statsVisible = false;
    private bool _monitorVisible = false;

    private bool _audioContextInitializeStarted;

    [Inject]
    public IJSRuntime Js { get; set; } = default!;

    [Inject]
    public HttpClient HttpClient { get; set; } = default!;

    [Inject]
    public NavigationManager NavManager { get; set; } = default!;

    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = default!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = default!;

    [Inject]
    public DotNet6502ConsoleLoggerConfiguration LoggerConfiguration { get; set; } = default!;

    [Inject]
    public GamepadList GamepadList { get; set; } = default!;

    [Inject]
    public IServiceProvider Services { get; set; } = default!;

    [Inject]
    public IEnumerable<ISystemShellPlugin> ShellPlugins { get; set; } = default!;

    [Inject]
    public IEnumerable<ISystemConfigurer> SystemConfigurers { get; set; } = default!;

    [Inject]
    public IEnumerable<ISkiaWasmRenderTargetPlugin> RenderTargetPlugins { get; set; } = default!;

    private ILogger _logger = default!;
    private readonly Dictionary<string, IWasmMenuContribution?> _menuContributionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IWasmHelpContribution?> _helpContributionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IWasmConfigDialogContribution?> _configContributionCache = new(StringComparer.OrdinalIgnoreCase);
    private IWasmMenuContribution? _activeMenuContribution;
    private IWasmHelpContribution? _activeHelpContribution;
    private IWasmConfigDialogContribution? _activeConfigContribution;
    private IDictionary<string, object> _hostViewParameters => new Dictionary<string, object>
    {
        ["Parent"] = this
    };

    protected override async Task OnInitializedAsync()
    {
        _logger = LoggerFactory.CreateLogger(nameof(Index));
        _logger.LogDebug("OnInitializedAsync() was called");

        // Any fatal startup error (no systems, an invalid DefaultEmulator, ...) shows a fatal
        // error message instead of the emulator UI. A browser tab cannot quit itself, so the
        // message has no action and cannot be dismissed.
        try
        {
            await InitializeEmulatorAsync();
            Initialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error during startup.");
            StartupError = "The emulator could not start.\n\n" + ex.Message;
        }
    }

    private async Task InitializeEmulatorAsync()
    {
        // A failure during Program.cs bootstrap (plug-in discovery / DI registration) — surface it
        // as a fatal startup error; the OnInitializedAsync catch turns it into the error screen.
        if (BootstrapError.Message != null)
            throw new InvalidOperationException(BootstrapError.Message);

        // Add systems
        var systemList = new SystemList();
        foreach (var systemConfigurer in SystemConfigurers)
            systemList.AddSystem(systemConfigurer);

        // Drop any system that declares no configuration variants — it cannot be built or run,
        // and would crash a variant picker. Treated as unavailable, like a missing plug-in.
        await systemList.RemoveSystemsWithNoConfigurationVariants(_logger);

        // No usable system — a fatal startup error.
        if (systemList.Systems.Count == 0)
            throw new InvalidOperationException(
                "No emulator systems are available. " +
                "Check the 'EnabledSystems' setting in appsettings.json, and that the system " +
                "plug-in projects are listed under TrimmerRootAssembly in the WASM app's .csproj.");

        // Add emulator config + system-specific host configs
        _emulatorConfig = new EmulatorConfig
        {
            DefaultEmulator = "C64",
            CurrentDrawScale = 2.0,
            Monitor = new()
            {
                MaxLineLength = 100,        // TODO: This affects help text printout, should it be set dynamically?

                //DefaultDirectory = "../../../../../../../samples/Assembler/Generic/Build"
                //DefaultDirectory = "%USERPROFILE%/source/repos/dotnet-6502/samples/Assembler/Generic/Build"
                //DefaultDirectory = "%HOME%/source/repos/dotnet-6502/samples/Assembler/Generic/Build"
            },
        };
        _emulatorConfig.Validate(systemList);

        // Create emulator host
        _wasmHost = new SkiaWASMHostApp(
            systemList,
            LoggerFactory,
            _emulatorConfig,
            () => _canvas,
            () => _grContext,
            () => _audioContext,
            GamepadList,
            Js,
            this,
            RenderTargetPlugins);

        _wasmHost.InitInputHandlerContext();

        // Set the default system
        await SelectSystem(_emulatorConfig.DefaultEmulator);
 
        // Set parameters from query string
        await SetDefaultsFromQueryParams(NavManager!.ToAbsoluteUri(NavManager.Uri));

        await RefreshAudioUiState();

        await SetElementVisibleState();
    }

    public async Task StateHasChangedCustom()
    {
        await SetElementVisibleState();

        base.StateHasChanged();
    }

    private Dictionary<string, string> _elementVisibleStates = new();
    private async Task SetElementVisibleState()
    {
        const string VISIBLE = "inline";
        const string VISIBLE_BLOCK = "inline-block";
        const string HIDDEN = "none";

        _elementVisibleStates["Canvas"] = CurrentEmulatorState != EmulatorState.Uninitialized ? VISIBLE : HIDDEN;
        _elementVisibleStates["CanvasUninitialized"] = CurrentEmulatorState == EmulatorState.Uninitialized ? VISIBLE_BLOCK : HIDDEN;
        _elementVisibleStates["Stats"] = _statsVisible ? VISIBLE : HIDDEN;
        _elementVisibleStates["Debug"] = _monitorVisible ? VISIBLE : HIDDEN;
        _elementVisibleStates["Monitor"] = _monitorVisible ? VISIBLE : HIDDEN;
        _elementVisibleStates["AudioVolume"] = _isAudioEnabled ? VISIBLE : HIDDEN;
    }

    private async Task SetDefaultsFromQueryParams(Uri uri)
    {
        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("systemName", out var systemName))
        {
            var systemNameParsed = systemName.ToString();
            if (systemNameParsed is not null && _wasmHost.AvailableSystemNames.Contains(systemNameParsed))
            {
                await SelectSystem(systemNameParsed);
            }
        }

        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("audioEnabled", out var audioEnabled))
        {
            if (bool.TryParse(audioEnabled, out bool audioEnabledParsed))
            {
                await _wasmHost.SetAudioEnabled(audioEnabledParsed);
                _isAudioEnabled = audioEnabledParsed;
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Workaround for audio context cannot be created before in OnInitializedAsync.
        // Must wait for _wasmHost to be created.
        if (!_audioContextInitializeStarted && _wasmHost != null)
        {
            _audioContextInitializeStarted = true;
            _logger.LogDebug("AudioContext initialized in OnAfterRenderAsync()");

            _audioContext = await AudioContextSync.CreateAsync(Js!);
            _wasmHost.InitAudioContext();
        }
    }

    private async Task SelectSystem(string systemName)
    {
        await _wasmHost.SelectSystem(systemName);
        UpdateActiveContributions();
        await RefreshAudioUiState();

        await SetConfigValidationMessage();

        await UpdateCanvasSize();

        await this.StateHasChangedCustom();
    }

    private async Task SelectedSystemChanged(string systemName)
    {
        await SelectSystem(systemName);
    }

    private async Task SelectSystemConfigurationVariantChanged(string systemConfigurationVariant)
    {
        await _wasmHost.SelectSystemConfigurationVariant(systemConfigurationVariant);
        await RefreshAudioUiState();

        await SetConfigValidationMessage();

        await UpdateCanvasSize();

        await this.StateHasChangedCustom();
    }

    public Task SelectSystemConfigurationVariant(string systemConfigurationVariant)
        => SelectSystemConfigurationVariantChanged(systemConfigurationVariant);

    public Task PersistCurrentHostSystemConfig()
        => _wasmHost.PersistCurrentHostSystemConfig();

    public void UpdateHostSystemConfig(IHostSystemConfig hostSystemConfig)
        => _wasmHost.UpdateHostSystemConfig(hostSystemConfig);

    public IEnumerable<Type> GetAvailableSystemRenderProviderTypes()
        => _wasmHost.GetAvailableSystemRenderProviderTypes();

    public IEnumerable<(Type renderProviderType, Type renderTargetType)> GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations()
        => _wasmHost.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations();

    public async Task UpdateCanvasSize()
    {
        bool isOk = await _wasmHost.IsSystemConfigValid();
        if (!isOk)
        {
            _windowWidthStyle = $"{EmulatorConfig.DEFAULT_CANVAS_WINDOW_WIDTH}px";
            _windowHeightStyle = $"{EmulatorConfig.DEFAULT_CANVAS_WINDOW_HEIGHT}px";
        }
        else
        {
            var system = await _wasmHost.GetSelectedSystem();
            if (system != null)
            {
                // Set SKGLView dimensions
                var screen = system.Screen;
                _windowWidthStyle = $"{screen.VisibleWidth * Scale}px";
                _windowHeightStyle = $"{screen.VisibleHeight * Scale}px";
            }
        }

        await this.StateHasChangedCustom();
    }

    private async Task SetConfigValidationMessage()
    {
        (bool isOk, List<string> validationErrors) = await _wasmHost.IsValidConfigWithDetails();
        if (!isOk)
            _selectedSystemConfigValidationMessage = string.Join(",", validationErrors!);
        else
            _selectedSystemConfigValidationMessage = "";
    }

    protected void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
    {
        if (CurrentEmulatorState != EmulatorState.Running)
            return;

        // Assume e.Surface.Canvas is not null and type GRContext
        var grContext = e.Surface.Context as GRContext;

        if (_canvas != e.Surface.Canvas || _grContext != grContext)
        {
            _logger.LogDebug("OnPaintSurface() was called with new canvas or context");

            if (_grContext != grContext)
            {
                _grContext?.Dispose();
                _grContext = grContext!;
            }
            if (_canvas != e.Surface.Canvas)
            {
                _canvas?.Dispose();
                _canvas = e.Surface.Canvas;
            }
        }

        // New render pipeline. Fire the render loop tick
        _wasmHost.RaiseRenderLoopTick();
    }

    /// <summary>
    /// Blazored.Modal instance required to open modal dialog.
    /// </summary>
    [CascadingParameter] public IModalService Modal { get; set; } = default!;

    public async Task ShowCurrentConfigUI()
    {
        if (_activeConfigContribution == null)
            return;

        var parameters = new ModalParameters
        {
            { "HostSystemConfig", _wasmHost.CurrentHostSystemConfig.Clone() },
            { "SelectedSystemConfigurationVariant", _wasmHost.SelectedSystemConfigurationVariant }
        };

        if (_activeConfigContribution.UseRenderProviderAndRenderTargetTypeCombinations)
        {
            parameters.Add("AvailableRendererProviderAndRenderTargetTypeCombinations", _wasmHost.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations());
        }
        else
        {
            // For other config UIs (like Generic), use the original parameter
            parameters.Add("RenderProviderTypes", _wasmHost.GetAvailableSystemRenderProviderTypes().ToArray());
        }

        var result = await Modal.Show(_activeConfigContribution.ComponentType, "Config", parameters).Result;

        if (result.Cancelled)
        {
            //Console.WriteLine("Modal was cancelled");
        }
        else if (result.Confirmed)
        {
            // Note: The UserSettings parameter that was sent to input dialog is by reference, so the data is updated directly by the dialog.
            //       Therefore no need to handle the result. 
            // TODO: Should the UserSettings be changed to be passed by value (struct instead of class?) instead to handle that the dialog can be cancelled, and then the changes won't stick?

            if (result.Data is null)
            {
                Console.WriteLine($"Returned null data");
                return;
            }
            //if (result.Data is not Dictionary<string, object>)
            //{
            //    Console.WriteLine($"Returned unrecognized type: {result.Data.GetType()}");
            //    return;
            //}
            //Dictionary<string, object> userSettings = (Dictionary<string, object>)result.Data;
            //Console.WriteLine($"Returned: {userSettings.Keys.Count} keys");

            var updatedHostSystemConfig = (IHostSystemConfig)result.Data;

            _wasmHost.UpdateHostSystemConfig(updatedHostSystemConfig);
            await _wasmHost.PersistCurrentHostSystemConfig();
        }

        await SetConfigValidationMessage();
        await UpdateCanvasSize();
        await this.StateHasChangedCustom();
    }


    private async Task ShowGeneralHelpUI() => await ShowHelpUI(typeof(GeneralHelpUI));
    private async Task ShowGeneralSettingsUI() => await ShowGeneralSettingsUI<GeneralSettingsUI>();

    public async Task ShowHelpUI(Type componentType)
    {
        var result = await Modal.Show(componentType, "Help").Result;

        if (result.Cancelled)
        {
            //Console.WriteLine("Modal was cancelled");
        }
        else if (result.Confirmed)
        {
        }
    }

    public async Task ShowGeneralSettingsUI<T>() where T : IComponent
    {
        var result = await Modal.Show<T>("Settings").Result;

        if (result.Cancelled)
        {
            //Console.WriteLine("Modal was cancelled");
        }
        else if (result.Confirmed)
        {
        }
    }


    public async Task SetDebugState(bool visible)
    {
        _debugVisible = visible;
        await FocusEmulator();
        await this.StateHasChangedCustom();
    }
    public async Task ToggleDebugState()
    {
        _debugVisible = !_debugVisible;
        await FocusEmulator();
        await this.StateHasChangedCustom();
    }
    public void UpdateDebug(string debug)
    {
        _debugString = debug;
        this.StateHasChanged();
    }

    public async Task SetStatsState(bool visible)
    {
        _statsVisible = visible;
        await FocusEmulator();
        await this.StateHasChangedCustom();
    }
    public async Task ToggleStatsState()
    {
        _statsVisible = !_statsVisible;
        // Assume to only run when emulator is running
        _wasmHost.CurrentRunningSystem!.InstrumentationEnabled = _statsVisible;
        await FocusEmulator();
        await this.StateHasChangedCustom();
    }
    public void UpdateStats(string stats)
    {
        _statsString = stats;
        this.StateHasChanged();
    }

    public async Task SetMonitorState(bool visible)
    {
        _monitorVisible = visible;
        if (visible)
            await FocusMonitor();
        else
            await FocusEmulator();
        await this.StateHasChangedCustom();
    }

    //private void BeforeUnload_BeforeUnloadHandler(object? sender, blazejewicz.Blazor.BeforeUnload.BeforeUnloadArgs e)
    //{
    //    _emulatorRenderer.Dispose();
    //}

    //public void Dispose()
    //{
    //    this.BeforeUnload.BeforeUnloadHandler -= BeforeUnload_BeforeUnloadHandler;
    //}

    private string _monitorOutput
    {
        get
        {
            if (_wasmHost == null || _wasmHost.Monitor == null)
                return "";
            return _wasmHost.Monitor.Output;
        }
        set
        {
            if (_wasmHost == null || _wasmHost.Monitor == null)
                return;
            _wasmHost.Monitor.Output = value;
        }
    }
    private string _monitorInput
    {
        get
        {
            if (_wasmHost == null || _wasmHost.Monitor == null)
                return "";
            return _wasmHost.Monitor.Input;
        }
        set
        {
            if (_wasmHost == null || _wasmHost.Monitor == null)
                return;
            _wasmHost.Monitor.Input = value;
        }
    }
    private string _monitorStatus
    {
        get
        {
            if (_wasmHost == null || _wasmHost.Monitor == null)
                return "";
            return _wasmHost.Monitor.Status;
        }
        set
        {
            if (_wasmHost == null || _wasmHost.Monitor == null)
                return;
            _wasmHost.Monitor.Status = value;
        }
    }

    private string GetDisplayStyle(string displayData)
    {
        const string VISIBLE = "inline";
        if (_elementVisibleStates.TryGetValue(displayData, out var displayStyle))
            return displayStyle;
        return VISIBLE;
    }

    public string GetSystemVisibilityDisplayStyle(string displayData, string systemName)
    {
        const string VISIBLE = "inline";
        //const string VISIBLE_BLOCK = "inline-block";
        const string HIDDEN = "none";

        switch (displayData)
        {
            case "Commands":
            {
                return _wasmHost.SelectedSystemName == systemName ? VISIBLE : HIDDEN;
            }
            case "Help":
            {
                return _wasmHost.SelectedSystemName == systemName ? VISIBLE : HIDDEN;
            }
            case "Config":
            {
                return _wasmHost.SelectedSystemName == systemName ? VISIBLE : HIDDEN;
            }
            default:
                return VISIBLE;
        }
    }

    private bool OnSelectSystemNameDisabled => CurrentEmulatorState == EmulatorState.Running || CurrentEmulatorState == EmulatorState.Paused;
    private bool OnStartDisabled => CurrentEmulatorState == EmulatorState.Running || !IsSelectedSystemConfigOk;
    private bool OnPauseDisabled => CurrentEmulatorState == EmulatorState.Paused || CurrentEmulatorState == EmulatorState.Uninitialized;
    private bool OnResetDisabled => CurrentEmulatorState == EmulatorState.Uninitialized;
    private bool OnStopDisabled => CurrentEmulatorState == EmulatorState.Uninitialized;

    public async Task OnStart(MouseEventArgs mouseEventArgs)
    {
        await StartAsync();
    }

    public async Task OnPause(MouseEventArgs mouseEventArgs)
    {
        await PauseAsync();
    }

    public async Task OnReset(MouseEventArgs mouseEventArgs)
    {
        await ResetAsync();
    }
    public async Task OnStop(MouseEventArgs mouseEventArgs)
    {
        await StopAsync();
    }
    private async Task OnMonitorToggle(MouseEventArgs mouseEventArgs)
    {
        _wasmHost!.ToggleMonitor();
        await this.StateHasChangedCustom();
    }

    private async Task OnStatsToggle(MouseEventArgs mouseEventArgs)
    {
        await ToggleStatsState();
        await ToggleDebugState();
    }

    /// <summary>
    /// Key pressed in running emulator canvas
    /// </summary>
    /// <param name="e"></param>
    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (_wasmHost == null)
            return;
        _wasmHost.OnKeyDown(e);
    }

    private void OnKeyUp(KeyboardEventArgs e)
    {
        if (_wasmHost == null)
            return;
        _wasmHost.OnKeyUp(e);
    }

    private void OnFocus(FocusEventArgs e)
    {
        if (_wasmHost == null)
            return;
        _wasmHost.OnFocus(e);
    }

    //private void OnPointerDown(PointerEventArgs e)
    //{
    //}

    //private void OnPointerMove(PointerEventArgs e)
    //{
    //}

    //private void OnPointerUp(PointerEventArgs e)
    //{
    //}

    //private void OnTouchMove(TouchEventArgs e)
    //{
    //}

    //private void OnMouseWheel(WheelEventArgs e)
    //{
    //}

    private void OnKeyDownMonitor(KeyboardEventArgs e)
    {
        if (_wasmHost == null || _wasmHost.Monitor == null)
            return;
        _wasmHost.Monitor.OnKeyDown(e);
    }

    private void OnKeyUpMonitor(KeyboardEventArgs e)
    {
        if (_wasmHost == null || _wasmHost.Monitor == null)
            return;
        _wasmHost.Monitor.OnKeyUp(e);
    }

    public async Task FocusEmulator()
    {
        await Js!.InvokeVoidAsync("focusId", "emulatorSKGLView", 100);  // Hack: Delay of x ms for focus to work.
    }

    private async Task FocusMonitor()
    {
        await Task.Run(async () => await _monitorInputRef.FocusAsync());    // Task.Run fix for focusing on a element that is not yet visible (but about to be)
        //await _monitorInputRef.FocusAsync();
    }

    /// <summary>
    /// Callback from monitor when user has selected a file to load
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    private async Task OnMonitorFilePickerChange(InputFileChangeEventArgs e)
    {
        if (_wasmHost == null || _wasmHost.Monitor == null)
            return;

        // Only expect one file
        if (e.FileCount > 1)
            return;
        var file = e.File;
        System.Diagnostics.Debug.WriteLine($"File picked: {file.Name} Size: {file.Size}");

        var fileBuffer = new byte[file.Size];
        await using var fileStream = file.OpenReadStream();
        await fileStream.ReadExactlyAsync(fileBuffer);
        //var fileSize = fileBuffer.Length;

        _wasmHost.Monitor.LoadBinaryFromUser(fileBuffer);
    }

    public async Task StartAsync()
    {
        await _wasmHost.Start();
        await UpdateCanvasSize();
        await FocusEmulator();
        await this.StateHasChangedCustom();
    }

    public async Task PauseAsync()
    {
        _wasmHost.Pause();
        await this.StateHasChangedCustom();
    }

    public async Task ResetAsync()
    {
        await _wasmHost.Reset();
        await this.StateHasChangedCustom();
    }

    public async Task StopAsync()
    {
        _wasmHost.Stop();
        await this.StateHasChangedCustom();
    }

    private void UpdateActiveContributions()
    {
        _activeMenuContribution = GetMenuContribution(_wasmHost.SelectedSystemName);
        _activeHelpContribution = GetHelpContribution(_wasmHost.SelectedSystemName);
        _activeConfigContribution = GetConfigContribution(_wasmHost.SelectedSystemName);
    }

    private IWasmMenuContribution? GetMenuContribution(string systemName)
    {
        if (!_menuContributionCache.TryGetValue(systemName, out var contribution))
        {
            contribution = ShellPlugins
                .FirstOrDefault(p => p.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase))
                ?.CreateMenuContribution(Services) as IWasmMenuContribution;
            _menuContributionCache[systemName] = contribution;
        }
        return contribution;
    }

    private IWasmHelpContribution? GetHelpContribution(string systemName)
    {
        if (!_helpContributionCache.TryGetValue(systemName, out var contribution))
        {
            contribution = ShellPlugins
                .FirstOrDefault(p => p.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase))
                ?.CreateInfoContribution(Services) as IWasmHelpContribution;
            _helpContributionCache[systemName] = contribution;
        }
        return contribution;
    }

    private IWasmConfigDialogContribution? GetConfigContribution(string systemName)
    {
        if (!_configContributionCache.TryGetValue(systemName, out var contribution))
        {
            contribution = ShellPlugins
                .FirstOrDefault(p => p.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase))
                ?.CreateConfigDialogContribution(Services) as IWasmConfigDialogContribution;
            _configContributionCache[systemName] = contribution;
        }
        return contribution;
    }
}
