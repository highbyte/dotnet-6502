using Blazored.LocalStorage;
using Blazored.Modal;
using Blazored.Modal.Services;
using Highbyte.DotNet6502.App.WASM.Emulator;
using Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;
using Highbyte.DotNet6502.App.WASM.Emulator.Skia;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.WebUtilities;
using Toolbelt.Blazor.Gamepad;
using Highbyte.DotNet6502.Systems.Logging.Console;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;

namespace Highbyte.DotNet6502.App.WASM.Pages;

public partial class Index
{
    //private string Version => typeof(Program).Assembly.GetName().Version!.ToString();
    private string Version => Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    /// <summary>
    /// Flag to indicate if the component has been initialized (set after OnInitializedAsync has been run) 
    /// to allow sub components to know if they can render code dependent on variables in this component.
    /// </summary>
    public bool Initialized { get; private set; } = false;

    private AudioContextSync _audioContext = default!;
    private SKCanvas _canvas = default!;
    private GRContext _grContext = default!;

    public EmulatorState CurrentEmulatorState => _wasmHost.EmulatorState;

    private bool IsSelectedSystemConfigOk => string.IsNullOrEmpty(_selectedSystemConfigValidationMessage);
    private string _selectedSystemConfigValidationMessage = "";

    private async Task<bool> AudioEnabledToggleDisabled()
    {
        if (_wasmHost == null)
            return true;
        var audioSupported = await _wasmHost.IsAudioSupported();
        var audioToggleDisabled = !audioSupported || CurrentEmulatorState == EmulatorState.Running || CurrentEmulatorState == EmulatorState.Paused;
        return audioToggleDisabled;
    }

    private async Task<bool> IsAudioEnabled()
    {
        if (_wasmHost == null)
            return false;
        return await _wasmHost.IsAudioEnabled();
    }

    private async Task SetAudioEnabled(bool enabled)
    {
        if (_wasmHost == null)
            return;
        await _wasmHost.SetAudioEnabled(enabled);
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
            UpdateCanvasSize();
        }
    }

    private ElementReference _monitorInputRef = default!;
    private EmulatorConfig _emulatorConfig = default!;

    private SkiaWASMHostApp _wasmHost = default!;
    public SkiaWASMHostApp WasmHost => _wasmHost;

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

    private ILogger<Index> _logger = default!;

    protected override async Task OnInitializedAsync()
    {
        _logger = LoggerFactory.CreateLogger<Index>();
        _logger.LogDebug("OnInitializedAsync() was called");

        var browserContext = new BrowserContext()
        {
            Uri = NavManager!.ToAbsoluteUri(NavManager.Uri),
            HttpClient = HttpClient,
            LocalStorage = LocalStorage
        };

        // Add systems
        var systemList = new SystemList<AspNetInputHandlerContext, WASMAudioHandlerContext>();

        var c64Setup = new C64Setup(browserContext, LoggerFactory);
        systemList.AddSystem(c64Setup);

        var genericComputerSetup = new GenericComputerSetup(browserContext, LoggerFactory);
        systemList.AddSystem(genericComputerSetup);

        // Add emulator config + system-specific host configs
        _emulatorConfig = new EmulatorConfig
        {
            DefaultEmulator = "C64",
            CurrentDrawScale = 2.0,
            Monitor = new()
            {
                MaxLineLength = 100,        // TODO: This affects help text printout, should it be set dynamically?

                //DefaultDirectory = "../../../../../../samples/Assembler/Generic/Build"
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
            this);

        _wasmHost.InitInputHandlerContext();

        // Set the default system
        await SelectSystem(_emulatorConfig.DefaultEmulator);
 
        // Set parameters from query string
        await SetDefaultsFromQueryParams(browserContext.Uri);

        await SetElementVisibleState();

        Initialized = true;
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
        _elementVisibleStates["AudioVolume"] = await IsAudioEnabled() ? VISIBLE : HIDDEN;
    }

    private async Task SetDefaultsFromQueryParams(Uri uri)
    {
        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("systemName", out var systemName))
        {
            var systemNameParsed = systemName.ToString();
            if (systemNameParsed is not null && _wasmHost.AvailableSystemNames.Contains(systemNameParsed))
            {
                await _wasmHost.SelectSystem(systemNameParsed);
            }
        }

        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("audioEnabled", out var audioEnabled))
        {
            if (bool.TryParse(audioEnabled, out bool audioEnabledParsed))
            {
                await _wasmHost.SetAudioEnabled(audioEnabledParsed);
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Workaround for audio context cannot be created before in OnInitializedAsync.
        // Must wait for _wasmHost to be created.
        if (!_audioContextInitializeStarted && _wasmHost != null && !_wasmHost.IsAudioHandlerContextInitialized)
        {
            _audioContextInitializeStarted = true;
            _logger.LogDebug("AudioContext initialized in OnAfterRenderAsync()");

            _audioContext = await AudioContextSync.CreateAsync(Js!);
            _wasmHost.InitAudioHandlerContext();
        }
    }

    private async Task SelectSystem(string systemName)
    {
        await _wasmHost.SelectSystem(systemName);

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

        await SetConfigValidationMessage();

        await UpdateCanvasSize();

        await this.StateHasChangedCustom();
    }

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

    public async Task ShowConfigUI<T>() where T : IComponent
    {
        var parameters = new ModalParameters
        {
            { "HostSystemConfig", _wasmHost.CurrentHostSystemConfig.Clone() },
            { "SelectedSystemConfigurationVariant", _wasmHost.SelectedSystemConfigurationVariant }
        };

        // Add the appropriate parameter based on the component type
        if (typeof(T).Name == "C64ConfigUI")
        {
            parameters.Add("AvailableRendererProviderAndRenderTargetTypeCombinations", _wasmHost.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations());
        }
        else
        {
            // For other config UIs (like Generic), use the original parameter
            parameters.Add("RenderProviderTypes", _wasmHost.GetAvailableSystemRenderProviderTypes().ToArray());
        }

        var result = await Modal.Show<T>("Config", parameters).Result;

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


    private async Task ShowGeneralHelpUI() => await ShowGeneralHelpUI<GeneralHelpUI>();
    private async Task ShowGeneralSettingsUI() => await ShowGeneralSettingsUI<GeneralSettingsUI>();

    public async Task ShowGeneralHelpUI<T>() where T : IComponent
    {
        var result = await Modal.Show<T>("Help").Result;

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
        await _wasmHost.Start();

        await FocusEmulator();

        await this.StateHasChangedCustom();
    }

    public async Task OnPause(MouseEventArgs mouseEventArgs)
    {
        _wasmHost!.Pause();
        await this.StateHasChangedCustom();
    }

    public async Task OnReset(MouseEventArgs mouseEventArgs)
    {
        await _wasmHost!.Reset();
        await this.StateHasChangedCustom();
    }
    public async Task OnStop(MouseEventArgs mouseEventArgs)
    {
        _wasmHost!.Stop();
        await this.StateHasChangedCustom();
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
        //var fileStream = e.File.OpenReadStream(file.Size);
        await file.OpenReadStream().ReadAsync(fileBuffer);
        //var fileSize = fileBuffer.Length;

        _wasmHost.Monitor.LoadBinaryFromUser(fileBuffer);
    }
}
