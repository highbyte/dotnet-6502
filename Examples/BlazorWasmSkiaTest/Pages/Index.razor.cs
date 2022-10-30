using BlazorWasmSkiaTest.Skia;
using Highbyte.DotNet6502.Systems.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using SkiaSharp.Views.Blazor;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Impl.AspNet;
using SkiaSharp;
using Highbyte.DotNet6502.Monitor;
using Microsoft.JSInterop;

namespace BlazorWasmSkiaTest.Pages
{
    public partial class Index
    {
        private const string DEFAULT_PRG_URL = "6502binaries/hostinteraction_scroll_text_and_cycle_colors.prg";
        //private const string DEFAULT_PRG_URL = "6502binaries/snake6502.prg";

        protected SKGLView? _emulatorSKGLViewRef;
        protected ElementReference? _mainRef;
        protected ElementReference? _monitorInputRef;

        private WasmHost? _wasmHost;
        private SystemList _systemList;

        private string _statsString = "Stats: calculating...";
        private string _debugString = "";

        private string _windowWidthStyle = "";
        private string _windowHeightStyle = "";

        private string _debugDisplay = "none"; // none or inline
        private string _monitorDisplay = "none"; // none or inline

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



        [Inject]
        public HttpClient? HttpClient { get; set; }

        [Inject]
        public NavigationManager? NavManager { get; set; }

        protected async override void OnInitialized()
        {
            var uri = NavManager!.ToAbsoluteUri(NavManager.Uri);

            var monitorConfig = new MonitorConfig
            {
                MaxLineLength = 100,        // TODO: This affects help text printout, should it be set dynamically?

                //DefaultDirectory = "../../../../../.cache/Examples/SadConsoleTest/AssemblerSource"
                //DefaultDirectory = "%USERPROFILE%/source/repos/dotnet-6502/.cache/Examples/SadConsoleTest/AssemblerSource"
                //DefaultDirectory = "%HOME%/source/repos/dotnet-6502/.cache/Examples/SadConsoleTest/AssemblerSource"
            };
            monitorConfig.Validate();


            var c64Config = await BuildC64Config(uri);
            //C64Config c64Config = null;

            var genericComputerConfig = await BuildGenericComputerConfig(uri);

            _systemList = new SystemList();
            _systemList.BuildSystemLookups(c64Config, genericComputerConfig);

            //var system = _systemList.Systems["Generic"];
            var system = _systemList.Systems["C64"];

            // Set SKGLView dimensions
            float scale = 3.0f;
            var screen = (IScreen)system;
            _windowWidthStyle = $"{screen.VisibleWidth * scale}px";
            _windowHeightStyle = $"{screen.VisibleHeight * scale}px";
            this.StateHasChanged();

            _wasmHost = new WasmHost(Js, system, GetSystemRunner, UpdateStats, UpdateDebug, SetMonitorState, monitorConfig, ToggleDebugStatsState, scale);

            //await FocusEmulator();
        }

        protected void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
        {
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

        private async Task<C64Config> BuildC64Config(Uri uri)
        {
            const string BASIC_ROM_URL = "ROM/basic.901226-01.bin";
            const string CHARGEN_ROM_URL = "ROM/characters.901225-01.bin";
            const string KERNAL_ROM_URL = "ROM/kernal.901227-03.bin";

            byte[] basicROMData = await GetROMFromUrl(BASIC_ROM_URL);
            byte[] chargenROMData = await GetROMFromUrl(CHARGEN_ROM_URL);
            byte[] kernalROMData = await GetROMFromUrl(KERNAL_ROM_URL);

            var c64Config = new C64Config
            {
                C64Model = "C64NTSC",   // C64NTSC, C64PAL
                Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
                // C64Model = "C64PAL",   // C64NTSC, C64PAL
                // Vic2Model = "PAL",     // NTSC, NTSC_old, PAL

                ROMDirectory = "",  // Set ROMDirectory to skip loading ROMs from file system (ROMDirectory + File property), instead read from the Data property
                ROMs = new List<ROM>
                {
                    new ROM
                    {
                        Name = "basic",
                        Data = basicROMData,
                        //Checksum = ""
                    },
                    new ROM
                    {
                        Name = "chargen",
                        Data = chargenROMData,
                        //Checksum = ""
                    },
                    new ROM
                    {
                        Name = "kernal",
                        Data = kernalROMData,
                        //Checksum = ""
                    }
                }
            };
            c64Config.Validate();

            return c64Config;
        }

        private async Task<byte[]> GetROMFromUrl(string url)
        {
            return await HttpClient!.GetByteArrayAsync(url);

            //var request = new HttpRequestMessage(HttpMethod.Get, url);
            ////request.SetBrowserRequestMode(BrowserRequestMode.NoCors);
            ////request.SetBrowserRequestCache(BrowserRequestCache.NoStore); //optional  

            ////var response = await HttpClient!.SendAsync(request);

            //var statusCode = response.StatusCode;
            //response.EnsureSuccessStatusCode();
            //byte[] responseRawData = await response.Content.ReadAsByteArrayAsync();
            //return responseRawData;
        }

        private async Task<GenericComputerConfig> BuildGenericComputerConfig(Uri uri)
        {

            // Load 6502 program binary specified in url
            var prgBytes = await Load6502Binary(uri);

            // Get screen size specified in url
            (int? cols, int? rows, ushort? screenMemoryAddress, ushort? colorMemoryAddress) = GetScreenSize(uri);

            cols = cols ?? 40;
            rows = rows ?? 25;
            screenMemoryAddress = screenMemoryAddress ?? 0x0400;
            colorMemoryAddress = colorMemoryAddress ?? 0xd800;

            var genericComputerConfig = new GenericComputerConfig
            {
                ProgramBinary = prgBytes,

                CPUCyclesPerFrame = 2500,
                ScreenRefreshFrequencyHz = 60,

                Memory = new EmulatorMemoryConfig
                {
                    Screen = new EmulatorScreenConfig
                    {
                        Cols = cols.Value,
                        Rows = rows.Value,
                        BorderCols = 3,
                        BorderRows = 3,
                        ScreenStartAddress = screenMemoryAddress.Value,
                        ScreenColorStartAddress = colorMemoryAddress.Value,

                        UseAscIICharacters = true,
                        DefaultBgColor = 0x00,     // 0x00 = Black (C64 scheme)
                        DefaultFgColor = 0x01,     // 0x0f = Light grey, 0x0e = Light Blue, 0x01 = White  (C64 scheme)
                        DefaultBorderColor = 0x0b, // 0x0b = Dark grey (C64 scheme)
                    },
                    Input = new EmulatorInputConfig
                    {
                        KeyPressedAddress = 0xd030,
                        KeyDownAddress = 0xd031,
                        KeyReleasedAddress = 0xd031,
                    }
                }
            };
            genericComputerConfig.Validate();

            return genericComputerConfig;
        }

        SystemRunner GetSystemRunner(ISystem system, SkiaRenderContext skiaRenderContext, AspNetInputHandlerContext inputHandlerContext)
        {
            if (system is C64 c64)
            {
                var renderer = (IRenderer<C64, SkiaRenderContext>)_systemList.Renderers[c64];
                renderer.Init(system, skiaRenderContext);

                var inputHandler = (IInputHandler<C64, AspNetInputHandlerContext>)_systemList.InputHandlers[c64];
                inputHandler.Init(system, inputHandlerContext);

                var systemRunnerBuilder = new SystemRunnerBuilder<C64, SkiaRenderContext, AspNetInputHandlerContext>(c64);
                var systemRunner = systemRunnerBuilder
                    .WithRenderer(renderer)
                    .WithInputHandler(inputHandler)
                    .Build();
                return systemRunner;
            }

            if (system is GenericComputer genericComputer)
            {
                var renderer = (IRenderer<GenericComputer, SkiaRenderContext>)_systemList.Renderers[genericComputer];
                renderer.Init(system, skiaRenderContext);

                var inputHandler = (IInputHandler<GenericComputer, AspNetInputHandlerContext>)_systemList.InputHandlers[genericComputer];
                inputHandler.Init(system, inputHandlerContext);

                var systemRunnerBuilder = new SystemRunnerBuilder<GenericComputer, SkiaRenderContext, AspNetInputHandlerContext>(genericComputer);
                var systemRunner = systemRunnerBuilder
                    .WithRenderer(renderer)
                    .WithInputHandler(inputHandler)
                    .Build();
                return systemRunner;
            }

            throw new NotImplementedException($"System not handled: {system.Name}");
        }


        private (int? cols, int? rows, ushort? screenMemoryAddress, ushort? colorMemoryAddress) GetScreenSize(Uri uri)
        {
            int? cols = null;
            int? rows = null;
            ushort? screenMemoryAddress = null;
            ushort? colorMemoryAddress = null;

            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("cols", out var colsParameter))
            {
                if (int.TryParse(colsParameter, out int colsParsed))
                    cols = colsParsed;
                else
                    cols = null;
            }
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("rows", out var rowsParameter))
            {
                if (int.TryParse(rowsParameter, out int rowsParsed))
                    rows = rowsParsed;
                else
                    rows = null;
            }
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("screenMem", out var screenMemParameter))
            {
                if (ushort.TryParse(screenMemParameter, out ushort screenMemParsed))
                    screenMemoryAddress = screenMemParsed;
                else
                    screenMemoryAddress = null;
            }
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("colorMem", out var colorMemParameter))
            {
                if (ushort.TryParse(colorMemParameter, out ushort colorMemParsed))
                    colorMemoryAddress = colorMemParsed;
                else
                    colorMemoryAddress = null;
            }

            return (cols, rows, screenMemoryAddress, colorMemoryAddress);

        }

        private async Task<byte[]> Load6502Binary(Uri uri)
        {
            byte[] prgBytes;
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("prgEnc", out var prgEnc))
            {
                // Query parameter prgEnc must be a valid Base64Url encoded string.
                // Examples on how to generate it from a compiled 6502 binary file:
                //      Linux: 
                //          base64 -w0 myprogram.prg | sed 's/+/-/g; s/\//_/g'

                //      https://www.base64encode.org/   
                //          - Encode files to Base64 format
                //          - Select file
                //          - Select options: BINARY, Perform URL-safe encoding (uses Base64Url format)
                //          - ENCODE
                //          - CLICK OR TAP HERE to download the encoded file
                //          - Use the contents in the generated file.
                //
                // Examples to generate a QR Code that will launch the program in the Base64URL string above:
                //      Linux:
                //          qrencode -s 3 -l L -o "myprogram.png" "http://localhost:5000/?prgEnc=THE_PROGRAM_ENCODED_AS_BASE64URL"
                prgBytes = Base64UrlDecode(prgEnc.ToString());
            }
            else if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("prgUrl", out var prgUrl))
            {
                prgBytes = await HttpClient!.GetByteArrayAsync(prgUrl.ToString());
            }
            else
            {
                prgBytes = await HttpClient!.GetByteArrayAsync(DEFAULT_PRG_URL);
            }

            return prgBytes;
        }

        /// <summary>
        /// Decode a Base64Url encoded string.
        /// The Base64Url standard is a bit different from normal Base64
        /// - Replaces '+' with '-'
        /// - Replaces '/' with '_'
        /// - Removes trailing '=' padding
        /// 
        /// This method does the above in reverse before decoding it as a normal Base64 string.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private byte[] Base64UrlDecode(string arg)
        {
            string s = arg;
            s = s.Replace('-', '+'); // 62nd char of encoding
            s = s.Replace('_', '/'); // 63rd char of encoding
            switch (s.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 2: s += "=="; break; // Two pad chars
                case 3: s += "="; break; // One pad char
                default:
                    throw new ArgumentException("Illegal base64url string!");
            }
            return Convert.FromBase64String(s); // Standard base64 decoder
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


    }
}
