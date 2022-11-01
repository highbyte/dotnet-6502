using BlazorWasmSkiaTest.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Microsoft.AspNetCore.Components;
using SkiaSharp;
using SkiaSharp.Views.Blazor;

namespace BlazorWasmSkiaTest.Pages
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
            }
        }

        protected SKGLView? _emulatorSKGLViewRef;
        protected ElementReference? _mainRef;
        protected ElementReference? _monitorInputRef;

        private MonitorConfig _monitorConfig;
        private SystemList _systemList;
        private WasmHost? _wasmHost;

        private string _statsString = "Stats: calculating...";
        private string _debugString = "";

        private string _windowWidthStyle = "";
        private string _windowHeightStyle = "";

        private string _debugDisplay = "none"; // none or inline
        private string _monitorDisplay = "none"; // none or inline

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
            _selectedSystemName = "C64";
        }

        private async Task InitEmulator()
        {
            var uri = NavManager!.ToAbsoluteUri(NavManager.Uri);
            var httpClient = HttpClient!;

            await _systemList.SetSelectedSystem(_selectedSystemName, httpClient, uri);

            // Set SKGLView dimensions
            float scale = 3.0f;
            var screen = (IScreen)_systemList.SelectedSystem!;
            _windowWidthStyle = $"{screen.VisibleWidth * scale}px";
            _windowHeightStyle = $"{screen.VisibleHeight * scale}px";
            this.StateHasChanged();

            _wasmHost = new WasmHost(Js, _systemList.SelectedSystem!, _systemList.GetSystemRunner, UpdateStats, UpdateDebug, SetMonitorState, _monitorConfig, ToggleDebugStatsState, scale);

            _emulatorState = EmulatorState.Paused;
            //await FocusEmulator();
        }

        private void CleanupEmulator()
        {
            _emulatorState = EmulatorState.Paused;
            _wasmHost?.Cleanup();
            _wasmHost = null;
            _emulatorState = EmulatorState.Uninitialized;
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
            if (_debugDisplay == "none")
                _debugDisplay = "inline";
            else
                _debugDisplay = "none";
            this.StateHasChanged();
        }

        protected async Task SetMonitorState(bool visible)
        {
            if (visible)
            {
                _monitorDisplay = "inline";
                await FocusMonitor();
            }
            else
            {
                _monitorDisplay = "none";
                await FocusEmulator();
            }
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
