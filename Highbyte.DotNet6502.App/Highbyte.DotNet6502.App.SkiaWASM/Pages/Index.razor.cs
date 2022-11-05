using Blazored.Modal.Services;
using Blazored.Modal;
using Highbyte.DotNet6502.App.SkiaWASM.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Microsoft.AspNetCore.Components;
using SkiaSharp;
using SkiaSharp.Views.Blazor;
using System.Xml.Schema;

namespace Highbyte.DotNet6502.App.SkiaWASM.Pages
{
    public partial class Index
    {
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
                ValidateEmulator();
            }
        }
        private Dictionary<string, SystemUserConfig> _systemUserConfigs = new();
        public SystemUserConfig SelectedSystemUserConfig
        {
            get
            {
                if (!_systemUserConfigs.ContainsKey(_selectedSystemName))
                    _systemUserConfigs.Add(_selectedSystemName, new SystemUserConfig());
                return _systemUserConfigs[_selectedSystemName];
            }
        }

        private bool IsSelectedSystemConfigOk => string.IsNullOrEmpty(_selectedSystemUserConfigValidationMessage);

        private string _selectedSystemUserConfigValidationMessage = "";
        public string GetSelectedSystemUserConfigValidationMessage()
        {
            if (string.IsNullOrEmpty(_selectedSystemUserConfigValidationMessage))
                return "";
            return _selectedSystemUserConfigValidationMessage;
        }

        protected SKGLView? _emulatorSKGLViewRef;
        protected ElementReference? _mainRef;
        protected ElementReference? _monitorInputRef;

        private MonitorConfig _monitorConfig;
        private SystemList _systemList;
        private WasmHost? _wasmHost;

        private string _statsString = "Stats: calculating...";
        private string _debugString = "";

        private const string DEFAULT_WINDOW_WIDTH_STYLE = "640px";
        private const string DEFAULT_WINDOW_HEIGHT_STYLE = "400px";

        private string _windowWidthStyle = DEFAULT_WINDOW_WIDTH_STYLE;
        private string _windowHeightStyle = DEFAULT_WINDOW_HEIGHT_STYLE;

        private bool _debugVisible = false;
        private bool _monitorVisible = false;

        [Inject]
        public HttpClient? HttpClient { get; set; }

        [Inject]
        public NavigationManager? NavManager { get; set; }

        protected async override void OnInitialized()
        {
            _monitorConfig = new MonitorConfig
            {
                MaxLineLength = 100,        // TODO: This affects help text printout, should it be set dynamically?

                //DefaultDirectory = "../../../../../.cache/Examples/SadConsoleTest/AssemblerSource"
                //DefaultDirectory = "%USERPROFILE%/source/repos/dotnet-6502/.cache/Examples/SadConsoleTest/AssemblerSource"
                //DefaultDirectory = "%HOME%/source/repos/dotnet-6502/.cache/Examples/SadConsoleTest/AssemblerSource"
            };
            _monitorConfig.Validate();

            _systemList = new SystemList();

            // Default system
            SelectedSystemName = "C64";
        }

        private void ValidateEmulator()
        {
            (bool isOk, string valError) = _systemList.IsSystemConfigOk(_selectedSystemName, SelectedSystemUserConfig);
            if (!isOk)
                _selectedSystemUserConfigValidationMessage = valError;
            else
                _selectedSystemUserConfigValidationMessage = "";
        }

        private async Task<bool> InitEmulator()
        {
            var uri = NavManager!.ToAbsoluteUri(NavManager.Uri);
            var httpClient = HttpClient!;

            SelectedSystemUserConfig.HttpClient = httpClient;
            SelectedSystemUserConfig.Uri = uri;

            await _systemList.SetSelectedSystem(_selectedSystemName, SelectedSystemUserConfig);

            // Set SKGLView dimensions
            float scale = 3.0f;
            var screen = (IScreen)_systemList.SelectedSystem!;
            _windowWidthStyle = $"{screen.VisibleWidth * scale}px";
            _windowHeightStyle = $"{screen.VisibleHeight * scale}px";
            this.StateHasChanged();

            _wasmHost = new WasmHost(Js, _systemList.SelectedSystem!, _systemList.GetSystemRunner, UpdateStats, UpdateDebug, SetMonitorState, _monitorConfig, ToggleDebugStatsState, scale);

            _emulatorState = EmulatorState.Paused;
            //await FocusEmulator();

            return true;
        }

        private void CleanupEmulator()
        {
            _emulatorState = EmulatorState.Paused;
            _wasmHost?.Cleanup();
            _wasmHost = null;
            _emulatorState = EmulatorState.Uninitialized;
            _windowWidthStyle = DEFAULT_WINDOW_WIDTH_STYLE;
            _windowHeightStyle = DEFAULT_WINDOW_HEIGHT_STYLE;
        }

        protected void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
        {
            if (_emulatorState != EmulatorState.Running)
                return;

            if (!(e.Surface.Context is GRContext grContext && grContext != null))
                return;

            if (_wasmHost == null)
                return;

            if (!_wasmHost.Initialized)
            {
                _wasmHost.Init(e.Surface.Canvas, grContext);
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
        //private async Task ShowGenericConfigUI() => await ShowConfigUI<GenericConfigUI>();

        private async Task ShowConfigUI<T>() where T : IComponent
        {
            var parameters = new ModalParameters()
                .Add("UserSettings", SelectedSystemUserConfig.UserSettings);

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

                //if (result.Data is null)
                //{
                //    Console.WriteLine($"Returned null data");
                //    return;
                //}
                //if (!(result.Data is Dictionary<string, object>))
                //{
                //    Console.WriteLine($"Returned unrecongnized type: {result.Data.GetType()}");
                //    return;
                //}
                //Dictionary<string, object> userSettings = (Dictionary<string, object>)result.Data;
                //Console.WriteLine($"Returned: {userSettings.Keys.Count} keys");
            }
            ValidateEmulator();
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
}
