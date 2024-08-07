using Microsoft.AspNetCore.Components;
using Blazored.Modal.Services;
using Blazored.Modal;
using Highbyte.DotNet6502.App.WASM.Skia;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Components.Forms;
using Blazored.LocalStorage;
using Highbyte.DotNet6502.Logging.Console;
using Toolbelt.Blazor.Gamepad;
using AutoMapper;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Generic;

namespace Highbyte.DotNet6502.App.WASM.Pages;

public partial class Index
{
    //private string Version => typeof(Program).Assembly.GetName().Version!.ToString();
    private string Version => Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    private BrowserContext _browserContext = default!;

    private AudioContextSync _audioContext = default!;

    public enum EmulatorState
    {
        Uninitialized,
        Running,
        Paused
    }
    public EmulatorState CurrentEmulatorState { get; private set; } = EmulatorState.Uninitialized;

    private string _selectedSystemName = default!;

    private bool IsSelectedSystemConfigOk => string.IsNullOrEmpty(_selectedSystemConfigValidationMessage);
    private string _selectedSystemConfigValidationMessage = "";

    // Note: The current config object (reference) is stored in this variable so that the UI can bind it's properties (not possible to use async call to _systemList.GetSystemConfig() in property )
    private ISystemConfig _currentSystemConfig = default!;
    public ISystemConfig SystemConfig => _currentSystemConfig;
    private IHostSystemConfig _currentHostSystemConfig = default!;
    public IHostSystemConfig HostSystemConfig => _currentHostSystemConfig;

    private bool AudioEnabledToggleDisabled => (
            (!(_currentSystemConfig?.AudioSupported ?? true)) ||
            (CurrentEmulatorState == EmulatorState.Running || CurrentEmulatorState == EmulatorState.Paused)
        );

    private bool AudioEnabled
    {
        get
        {
            return _currentSystemConfig?.AudioEnabled ?? false;
        }
        set
        {
            _currentSystemConfig.AudioEnabled = value;
        }
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
            _wasmHost?.AudioHandlerContext.SetMasterVolumePercent(_masterVolumePercent);
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
    private SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext> _systemList = default!;

    private WasmHost _wasmHost = default!;
    public WasmHost WasmHost => _wasmHost;

    private string _statsString = "Instrumentations: calculating...";
    private string _debugString = "";

    private string _windowWidthStyle = "0px";
    private string _windowHeightStyle = "0px";

    private bool _debugVisible = false;
    private bool _monitorVisible = false;

    [Inject]
    public IJSRuntime? Js { get; set; }

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
    private IMapper _mapper = default!;

    public bool Initialized { get; private set; } = false;
    protected override async Task OnInitializedAsync()
    {
        _logger = LoggerFactory.CreateLogger<Index>();
        _logger.LogDebug("OnInitializedAsync() was called");

        _browserContext = new()
        {
            Uri = NavManager!.ToAbsoluteUri(NavManager.Uri),
            HttpClient = HttpClient,
            LocalStorage = LocalStorage
        };

        // Add systems
        _systemList = new SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext>();

        var c64HostConfig = new C64HostConfig
        {
            Renderer = C64HostRenderer.SkiaSharp,
        };
        var c64Setup = new C64Setup(_browserContext, LoggerFactory, c64HostConfig);
        _systemList.AddSystem(c64Setup);

        var genericComputerHostConfig = new GenericComputerHostConfig();
        var genericComputerSetup = new GenericComputerSetup(_browserContext, LoggerFactory, genericComputerHostConfig);
        _systemList.AddSystem(genericComputerSetup);

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
            HostSystemConfigs = new Dictionary<string, IHostSystemConfig>
            {
                { C64.SystemName, c64HostConfig },
                { GenericComputer.SystemName, new GenericComputerHostConfig() }
            }
        };
        _emulatorConfig.Validate(_systemList);

        // Default system
        await SelectEmulator(_emulatorConfig.DefaultEmulator);

        // Set parameters from query string
        await SetDefaultsFromQueryParams(_browserContext.Uri);

        // TODO: Make Automapper configuration more generic, incorporate in classes that need it?
        var mapperConfiguration = new MapperConfiguration(
            cfg =>
            {
                cfg.CreateMap<C64HostConfig, C64HostConfig>();
            }
        );
        _mapper = mapperConfiguration.CreateMapper();

        Initialized = true;
    }

    private async Task SetDefaultsFromQueryParams(Uri uri)
    {
        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("systemName", out var systemName))
        {
            var systemNameParsed = systemName.ToString();
            if (systemNameParsed is not null && _systemList.Systems.Contains(systemNameParsed))
            {
                await SelectEmulator(systemNameParsed);
            }
        }

        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("audioEnabled", out var audioEnabled))
        {
            if (bool.TryParse(audioEnabled, out bool audioEnabledParsed))
            {
                AudioEnabled = audioEnabledParsed;
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _audioContext = await AudioContextSync.CreateAsync(Js!);
        }
    }

    //protected override async void OnAfterRender(bool firstRender)
    //{
    //    if (firstRender)
    //    {
    //        //await FocusEmulator();
    //    }
    //}

    private async Task OnSelectedEmulatorChanged(ChangeEventArgs e)
    {
        var systemName = e.Value?.ToString() ?? "";
        await SelectEmulator(systemName);
    }

    private async Task SelectEmulator(string systemName)
    {
        _selectedSystemName = systemName;

        (bool isOk, List<string> validationErrors) = await _systemList.IsValidConfigWithDetails(_selectedSystemName);
        if (!isOk)
            _selectedSystemConfigValidationMessage = string.Join(",", validationErrors!);
        else
            _selectedSystemConfigValidationMessage = "";

        _currentSystemConfig = await _systemList.GetCurrentSystemConfig(_selectedSystemName);
        _currentHostSystemConfig = _emulatorConfig.HostSystemConfigs[_selectedSystemName];

        UpdateCanvasSize();
        this.StateHasChanged();

        _logger.LogInformation($"System selected: {_selectedSystemName}");
    }

    private async void UpdateCanvasSize()
    {
        bool isOk = await _systemList.IsValidConfig(_selectedSystemName);
        if (!isOk)
        {
            _windowWidthStyle = $"{EmulatorConfig.DEFAULT_CANVAS_WINDOW_WIDTH}px";
            _windowHeightStyle = $"{EmulatorConfig.DEFAULT_CANVAS_WINDOW_HEIGHT}px";
        }
        else
        {
            var system = await _systemList.GetSystem(_selectedSystemName);
            // Set SKGLView dimensions
            var screen = system.Screen;
            _windowWidthStyle = $"{screen.VisibleWidth * Scale}px";
            _windowHeightStyle = $"{screen.VisibleHeight * Scale}px";
        }

        this.StateHasChanged();
    }

    private void InitEmulator()
    {
        _wasmHost = new WasmHost(Js!, _selectedSystemName, _systemList, UpdateStats, UpdateDebug, SetMonitorState, _emulatorConfig, ToggleDebugStatsState, LoggerFactory, (float)Scale, MasterVolumePercent);
        CurrentEmulatorState = EmulatorState.Paused;
    }

    private void CleanupEmulator()
    {
        _debugVisible = false;
        _wasmHost!.Monitor.Disable();

        CurrentEmulatorState = EmulatorState.Paused;
        _wasmHost?.Cleanup();
        _wasmHost = default!;
        CurrentEmulatorState = EmulatorState.Uninitialized;
    }

    protected async void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
    {
        if (CurrentEmulatorState != EmulatorState.Running)
            return;

        if (!(e.Surface.Context is GRContext grContext && grContext != null))
            return;

        if (_wasmHost == null)
            return;

        if (!_wasmHost.Initialized)
        {
            await _wasmHost.Init(e.Surface.Canvas, grContext, _audioContext, GamepadList, Js!);
        }

        //_emulatorRenderer.SetSize(e.Info.Width, e.Info.Height);
        //if (e.Surface.Context is GRContext context && context != null)
        //{
        //    // If we draw our own images (not directly on the canvas provided), make sure it's within the same contxt
        //    _emulatorRenderer.SetContext(context);
        //}

        _wasmHost.Render(e.Surface.Canvas, grContext);
    }

    internal async Task UpdateCurrentSystemConfig(ISystemConfig config, IHostSystemConfig? hostSystemConfig)
    {
        // Update the system config
        await _systemList.PersistNewSystemConfig(_selectedSystemName, config);
        _currentSystemConfig = config;

        // Update the existing host system config, it is referenced from different objects (thus we cannot replace it with a new one).
        if (hostSystemConfig != null)
        {
            if (_currentHostSystemConfig != null && hostSystemConfig != null)
                _mapper.Map(hostSystemConfig, _currentHostSystemConfig);
        }
    }

    /// <summary>
    /// Blazored.Modal instance required to open modal dialog.
    /// </summary>
    [CascadingParameter] public IModalService Modal { get; set; } = default!;

    public async Task ShowConfigUI<T>() where T : IComponent
    {
        var parameters = new ModalParameters()
            .Add("SystemConfig", _currentSystemConfig)
            .Add("HostSystemConfig", _currentHostSystemConfig);

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
            //    Console.WriteLine($"Returned unrecongnized type: {result.Data.GetType()}");
            //    return;
            //}
            //Dictionary<string, object> userSettings = (Dictionary<string, object>)result.Data;
            //Console.WriteLine($"Returned: {userSettings.Keys.Count} keys");

            var resultData = ((ISystemConfig UpdatedSystemConfig, IHostSystemConfig UpdatedHostSystemConfig))result.Data;
            await UpdateCurrentSystemConfig(resultData.UpdatedSystemConfig, resultData.UpdatedHostSystemConfig);
        }

        (bool isOk, List<string> validationErrors) = await _systemList.IsValidConfigWithDetails(_selectedSystemName);
        _selectedSystemConfigValidationMessage = "";
        if (!isOk)
        {
            _selectedSystemConfigValidationMessage = string.Join(",", validationErrors);
        }

        UpdateCanvasSize();
        this.StateHasChanged();
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

    private void UpdateStats(string stats)
    {
        _statsString = stats;
        this.StateHasChanged();
    }

    private void UpdateDebug(string debug)
    {
        _debugString = debug;
        this.StateHasChanged();
    }

    private async Task ToggleDebugStatsState()
    {
        _debugVisible = !_debugVisible;
        _wasmHost.SystemRunner.System.InstrumentationEnabled = _debugVisible;
        await FocusEmulator();
        this.StateHasChanged();
    }

    private async Task SetMonitorState(bool visible)
    {
        _monitorVisible = visible;
        if (visible)
            await FocusMonitor();
        else
            await FocusEmulator();
        this.StateHasChanged();
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
        const string VISIBLE_BLOCK = "inline-block";
        const string HIDDEN = "none";

        switch (displayData)
        {
            case "Canvas":
            {
                return CurrentEmulatorState != EmulatorState.Uninitialized ? VISIBLE : HIDDEN;
            }
            case "CanvasUninitialized":
            {
                return CurrentEmulatorState == EmulatorState.Uninitialized ? VISIBLE_BLOCK : HIDDEN;
            }
            case "Debug":
            {
                return _debugVisible ? VISIBLE : HIDDEN;
            }
            case "Monitor":
            {
                return _monitorVisible ? VISIBLE : HIDDEN;
            }
            case "AudioVolume":
            {
                return (_currentSystemConfig?.AudioEnabled ?? false) ? VISIBLE : HIDDEN;
            }
            default:
                return VISIBLE;
        }
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
                return _selectedSystemName == systemName ? VISIBLE : HIDDEN;
            }
            case "Help":
            {
                return _selectedSystemName == systemName ? VISIBLE : HIDDEN;
            }
            case "Config":
            {
                return _selectedSystemName == systemName ? VISIBLE : HIDDEN;
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
        if (CurrentEmulatorState == EmulatorState.Uninitialized)
        {
            bool isOk = await _systemList.IsValidConfig(_selectedSystemName);
            if (!isOk)
                return;

            // TODO: Is forcing a full GC a good idea in Blazor WASM?
            // Force a full GC to free up memory, so it won't risk accumulate memory usage if GC has not run for a while.
            var m0 = GC.GetTotalMemory(forceFullCollection: true);
            _logger.LogInformation("Allocated memory before starting emulator: " + m0);

            InitEmulator();
        }

        _wasmHost.Start();
        CurrentEmulatorState = EmulatorState.Running;
        await FocusEmulator();

        this.StateHasChanged();

        _logger.LogInformation($"System started: {_selectedSystemName}");
    }

    public void OnPause(MouseEventArgs mouseEventArgs)
    {
        if (CurrentEmulatorState == EmulatorState.Uninitialized)
            return;

        _wasmHost!.Stop();
        CurrentEmulatorState = EmulatorState.Paused;
        this.StateHasChanged();

        _logger.LogInformation($"System paused: {_selectedSystemName}");
    }

    public async Task OnReset(MouseEventArgs mouseEventArgs)
    {
        OnPause(mouseEventArgs);
        OnStop(mouseEventArgs);
        await OnStart(mouseEventArgs);
    }
    public void OnStop(MouseEventArgs mouseEventArgs)
    {
        CleanupEmulator();
        this.StateHasChanged();
        _logger.LogInformation($"System stopped: {_selectedSystemName}");

    }
    private void OnMonitorToggle(MouseEventArgs mouseEventArgs)
    {
        _wasmHost!.ToggleMonitor();
        this.StateHasChanged();
    }

    private async Task OnStatsToggle(MouseEventArgs mouseEventArgs)
    {
        await ToggleDebugStatsState();
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (_wasmHost == null || _wasmHost.InputHandlerContext == null)
            return;
        _wasmHost.InputHandlerContext.KeyDown(e);

        // Emulator host functions such as monitor and stats/debug
        _wasmHost.OnKeyDown(e);
    }

    private void OnKeyUp(KeyboardEventArgs e)
    {
        if (_wasmHost == null || _wasmHost.InputHandlerContext == null)
            return;
        _wasmHost.InputHandlerContext.KeyUp(e);
    }

    private void OnFocus(FocusEventArgs e)
    {
        if (_wasmHost == null || _wasmHost.InputHandlerContext == null)
            return;
        _wasmHost.InputHandlerContext.OnFocus(e);
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

    private async Task FocusEmulator()
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
