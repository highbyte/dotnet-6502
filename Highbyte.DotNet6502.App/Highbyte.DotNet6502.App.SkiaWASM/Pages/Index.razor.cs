using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Blazored.Modal.Services;
using Blazored.Modal;
using Highbyte.DotNet6502.App.SkiaWASM.Skia;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Impl.AspNet.Commodore64;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

namespace Highbyte.DotNet6502.App.SkiaWASM.Pages;

public partial class Index
{
    //public string Version => typeof(Program).Assembly.GetName().Version!.ToString();
    public string Version => Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

    private BrowserContext _browserContext;

    //private AudioContext _audioContext;
    private AudioContextSync _audioContext;

    public enum EmulatorState
    {
        Uninitialized,
        Running,
        Paused
    }

    private EmulatorState _emulatorState = EmulatorState.Uninitialized;
    private string _selectedSystemName;
    public string SelectedSystemName
    {
        get
        {
            return _selectedSystemName;
        }
        set
        {
            _selectedSystemName = value;
            OnSelectedEmulatorChanged();
        }
    }

    private bool IsSelectedSystemConfigOk => string.IsNullOrEmpty(_selectedSystemConfigValidationMessage);

    private string _selectedSystemConfigValidationMessage = "";
    public string GetSelectedSystemConfigValidationMessage()
    {
        if (string.IsNullOrEmpty(_selectedSystemConfigValidationMessage))
            return "";
        return _selectedSystemConfigValidationMessage;
    }

    private double _scale = 2.0f;
    private double Scale
    {
        get
        {
            return _scale;
        }
        set
        {
            _scale = value;
            UpdateCanvasSize();
        }
    }

    protected SKGLView? _emulatorSKGLViewRef;
    protected ElementReference? _mainRef;
    protected ElementReference? _monitorInputRef;

    private MonitorConfig _monitorConfig;
    private SystemList<SkiaRenderContext, AspNetInputHandlerContext, C64WASMSoundHandlerContext> _systemList;
    private WasmHost? _wasmHost;

    private string _statsString = "Stats: calculating...";
    private string _debugString = "";

    private const int DEFAULT_WINDOW_WIDTH = 640;
    private const int DEFAULT_WINDOW_HEIGHT = 400;

    private string _windowWidthStyle = "0px";
    private string _windowHeightStyle = "0px";

    private bool _debugVisible = false;
    private bool _monitorVisible = false;

    [Inject]
    public HttpClient? HttpClient { get; set; }

    [Inject]
    public NavigationManager? NavManager { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _browserContext = new()
        {
            Uri = NavManager!.ToAbsoluteUri(NavManager.Uri),
            HttpClient = HttpClient!,
            LocalStorage = _localStorage
        };

        _monitorConfig = new()
        {
            MaxLineLength = 100,        // TODO: This affects help text printout, should it be set dynamically?

            //DefaultDirectory = "../../../../../Examples/Assembler/Generic/Build"
            //DefaultDirectory = "%USERPROFILE%/source/repos/dotnet-6502/Examples/Assembler/Generic/Build"
            //DefaultDirectory = "%HOME%/source/repos/dotnet-6502/Examples/Assembler/Generic/Build"
        };
        _monitorConfig.Validate();

        _systemList = new SystemList<SkiaRenderContext, AspNetInputHandlerContext, C64WASMSoundHandlerContext>();

        var c64Setup = new C64Setup(_browserContext);
        await _systemList.AddSystem(C64.SystemName, c64Setup.BuildSystem, c64Setup.BuildSystemRunner, c64Setup.GetNewConfig, c64Setup.PersistConfig);

        var genericComputerSetup = new GenericComputerSetup(_browserContext);
        await _systemList.AddSystem(GenericComputer.SystemName, genericComputerSetup.BuildSystem, genericComputerSetup.BuildSystemRunner, genericComputerSetup.GetNewConfig, genericComputerSetup.PersistConfig);

        // Default system
        SelectedSystemName = C64.SystemName;

    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _audioContext = await AudioContextSync.CreateAsync(Js);
        }
    }

    //protected override async void OnAfterRender(bool firstRender)
    //{
    //    if (firstRender)
    //    {
    //        //await FocusEmulator();
    //    }
    //}


    private async void OnSelectedEmulatorChanged()
    {
        (bool isOk, List<string> validationErrors) = await _systemList.IsValidConfigWithDetails(_selectedSystemName);
        if (!isOk)
            _selectedSystemConfigValidationMessage = string.Join(",", validationErrors);
        else
            _selectedSystemConfigValidationMessage = "";

        UpdateCanvasSize();
        this.StateHasChanged();
    }

    private async void UpdateCanvasSize()
    {
        bool isOk = await _systemList.IsValidConfig(_selectedSystemName);
        if (!isOk)
        {
            _windowWidthStyle = $"{DEFAULT_WINDOW_WIDTH}px";
            _windowHeightStyle = $"{DEFAULT_WINDOW_HEIGHT}px";
        }
        else
        {
            var system = await _systemList.GetSystem(_selectedSystemName);
            // Set SKGLView dimensions
            var screen = (IScreen)system;
            _windowWidthStyle = $"{screen.VisibleWidth * Scale}px";
            _windowHeightStyle = $"{screen.VisibleHeight * Scale}px";
        }

        this.StateHasChanged();
    }

    private async Task InitEmulator()
    {
        _wasmHost = new WasmHost(Js, _selectedSystemName, _systemList, UpdateStats, UpdateDebug, SetMonitorState, _monitorConfig, ToggleDebugStatsState, (float)Scale);
        _emulatorState = EmulatorState.Paused;
    }

    private async Task CleanupEmulator()
    {
        _debugVisible = false;
        await _wasmHost!.Monitor.Disable();

        _emulatorState = EmulatorState.Paused;
        _wasmHost?.Cleanup();
        _wasmHost = null;
        _emulatorState = EmulatorState.Uninitialized;
    }

    protected async void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
    {
        if (_emulatorState != EmulatorState.Running)
            return;

        if (!(e.Surface.Context is GRContext grContext && grContext != null))
            return;

        if (_wasmHost == null)
            return;

        if (!_wasmHost.Initialized)
        {
            await _wasmHost.Init(e.Surface.Canvas, grContext, _audioContext, Js);
        }

        //_emulatorRenderer!.SetSize(e.Info.Width, e.Info.Height);
        //if (e.Surface.Context is GRContext context && context != null)
        //{
        //    // If we draw our own images (not directly on the canvas provided), make sure it's within the same contxt
        //    _emulatorRenderer.SetContext(context);
        //}

        _wasmHost.Render(e.Surface.Canvas, grContext);
    }

    /// <summary>
    /// Blazored.Modal instance required to open modal dialog.
    /// </summary>
    [CascadingParameter] public IModalService Modal { get; set; } = default!;

    private async Task ShowC64ConfigUI() => await ShowConfigUI<C64ConfigUI>();
    private async Task ShowGenericConfigUI() => await ShowConfigUI<GenericConfigUI>();

    private async Task ShowConfigUI<T>() where T : IComponent
    {
        var systemConfig = await _systemList.GetCurrentSystemConfig(_selectedSystemName);
        var parameters = new ModalParameters()
            .Add("SystemConfig", systemConfig);

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

            var updatedSystemConfig = (ISystemConfig)result.Data;

            await _systemList.PersistNewSystemConfig(_selectedSystemName, updatedSystemConfig);
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

    private async Task ShowC64HelpUI() => await ShowHelpUI<C64HelpUI>();
    private async Task ShowGenericHelpUI() => await ShowHelpUI<GenericHelpUI>();
    private async Task ShowGeneralHelpUI() => await ShowHelpUI<GeneralHelpUI>();

    private async Task ShowHelpUI<T>() where T : IComponent
    {
        var result = await Modal.Show<T>("Help").Result;

        if (result.Cancelled)
        {
            //Console.WriteLine("Modal was cancelled");
        }
        else if (result.Confirmed)
        {
            //Console.WriteLine($"Returned: {userSettings.Keys.Count} keys");
        }
    }

    protected void UpdateStats(string stats)
    {
        _statsString = stats;
        this.StateHasChanged();
    }

    protected void UpdateDebug(string debug)
    {
        _debugString = debug;
        this.StateHasChanged();
    }

    protected async Task ToggleDebugStatsState()
    {
        _debugVisible = !_debugVisible;
        await FocusEmulator();
        this.StateHasChanged();
    }

    protected async Task SetMonitorState(bool visible)
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
}
