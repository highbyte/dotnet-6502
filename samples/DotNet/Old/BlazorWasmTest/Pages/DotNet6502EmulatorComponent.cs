using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Generic;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using BlazorWasmTest.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorWasmTest
{
    public class DotNet6502EmulatorComponent : ComponentBase
    {
        [Inject]
        protected IJSRuntime _jSRuntime {get; set;}

        [Inject]
        protected HttpClient _httpClient {get; set;}

        [Inject]
        protected NavigationManager _navManager {get; set;}


        protected ElementReference myReference;  // set the @ref for attribute        

        /// <summary>
        /// Map of C64 color value (byte) to css classes for color and background-color styles
        /// </summary>
        /// <returns></returns>
        static Dictionary<byte, Tuple<string,string>> C64ColorMap = new()
        {
            { 0x00, new Tuple<string,string>("c64_black_fg",         "c64_black_bg")},        // Black
            { 0x01, new Tuple<string,string>("c64_white_fg",         "c64_white_bg")},        // White
            { 0x02, new Tuple<string,string>("c64_red_fg",           "c64_red_bg")},          // Red
            { 0x03, new Tuple<string,string>("c64_cyan_fg",          "c64_cyan_bg")},         // Cyan
            { 0x04, new Tuple<string,string>("c64_purple_fg",        "c64_purple_bg")},       // Violet/purple
            { 0x05, new Tuple<string,string>("c64_green_fg",         "c64_green_bg")},        // Green
            { 0x06, new Tuple<string,string>("c64_blue_fg",          "c64_blue_bg")},         // Blue
            { 0x07, new Tuple<string,string>("c64_yellow_fg",        "c64_yellow_bg")},       // Yellow
            { 0x08, new Tuple<string,string>("c64_orange_fg",        "c64_orange_bg")},       // Orange
            { 0x09, new Tuple<string,string>("c64_brown_fg",         "c64_brown_bg")},        // Brown
            { 0x0a, new Tuple<string,string>("c64_lightred_fg",      "c64_lightred_bg")},     // Light red
            { 0x0b, new Tuple<string,string>("c64_darkgrey_fg",      "c64_darkgrey_bg")},     // Dark grey
            { 0x0c, new Tuple<string,string>("c64_grey_fg",          "c64_grey_bg")},         // Grey
            { 0x0d, new Tuple<string,string>("c64_lightgreen_fg",    "c64_lightgreen_bg")},   // Light green
            { 0x0e, new Tuple<string,string>("c64_lightblue_fg",     "c64_lightblue_bg")},    // Light blue
            { 0x0f, new Tuple<string,string>("c64_lightgrey_fg",     "c64_lightgrey_bg")},    // Light grey
        };

        private ulong _frameCounter = 0;
        const int UPDATE_EVERY_X_FRAME = 0;

        const string DEFAULT_PRG_URL = "6502binaries/hostinteraction_scroll_text_and_cycle_colors.prg";
        //const string DEFAULT_PRG_URL = "6502binaries/hello_world.prg";

        protected const string DEFAULT_ROOT_CSS_CLASS = "c64";
        protected const string DEFAULT_BORDER_CSS_CLASS = "c64frame";

        protected const int DEFAULT_MAX_COLS = 40;
        protected const int DEFAULT_MAX_ROWS = 25;

        const ushort DEFAULT_SCREEN_MEMORY_ADDRESS = 0x0400;
        const ushort DEFAULT_COLOR_MEMORY_ADDRESS  = 0xd800;
        const ushort DEFAULT_BORDER_COLOR_ADDRESS = 0xd020; 
        const ushort DEFAULT_BACKGROUND_COLOR_ADDRESS = 0xd021;

        
        protected string ROOT_CSS_CLASS = DEFAULT_ROOT_CSS_CLASS;
        protected string BORDER_CSS_CLASS = DEFAULT_BORDER_CSS_CLASS;
        protected int MAX_COLS = DEFAULT_MAX_COLS;
        protected int MAX_ROWS = DEFAULT_MAX_ROWS;

        protected ushort SCREEN_MEMORY_ADDRESS = DEFAULT_SCREEN_MEMORY_ADDRESS;
        protected ushort COLOR_MEMORY_ADDRESS  = DEFAULT_COLOR_MEMORY_ADDRESS;
        protected ushort BORDER_COLOR_ADDRESS = DEFAULT_BORDER_COLOR_ADDRESS; 
        protected ushort BACKGROUND_COLOR_ADDRESS = DEFAULT_BACKGROUND_COLOR_ADDRESS;


        // Memory address in emulator that the 6502 program and the host will use to communicate if current frame is done or not.
        const ushort SCREEN_REFRESH_STATUS_ADDRESS = 0xd000;

        // Currently pressed key on host (ASCII byte). If no key is pressed, value is 0x00
        const ushort KEY_PRESSED_ADDRESS = 0xd030;
        // Currently down key on host (ASCII byte). If no key is down, value is 0x00
        const ushort KEY_DOWN_ADDRESS = 0xd031;
        // Currently released key on host (ASCII byte). If no key is down, value is 0x00
        const ushort KEY_RELEASED_ADDRESS = 0xd031;

        // Memory address to store a randomly generated value between 0-255
        const ushort RANDOM_VALUE_ADDRESS = 0xd41b;
        private Random _rnd = new Random();

        private GenericComputer _computer = null;

        protected bool ShowDebugMessages = false;
        private List<string> _debugMessages;

        protected override async Task OnInitializedAsync()
        {
            byte[] prgBytes;
            var uri = _navManager.ToAbsoluteUri(_navManager.Uri);
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
                prgBytes = await _httpClient.GetByteArrayAsync(prgUrl.ToString());
            }
            else
            {
                prgBytes = await _httpClient.GetByteArrayAsync(DEFAULT_PRG_URL);
            }

            // TODO: Customize screen dimensions. Also needs to have predefined CSS styles for different widths? Customize screen memory addresses? Or always assume C64 addresses and layout? 
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("cols", out var cols))
            {
                if(int.TryParse(cols, out int colsParsed))
                {
                    // Only specific column sizes supported currently because each needs a specific css class (TODO: could be dynamic?).
                    // Default is cols 40.
                    if(colsParsed==40 || colsParsed==32)
                        MAX_COLS = colsParsed;
                    else
                        throw new Exception($"Unsupported # columns: {colsParsed}");
                }
                else
                    MAX_COLS = DEFAULT_MAX_COLS;

                // Set root and border css classes based on cols (default is already set for 40)
                if(MAX_COLS==32)
                {
                    ROOT_CSS_CLASS = "c64-32";
                    BORDER_CSS_CLASS = "c64frame-32";
                }
            }
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("rows", out var rows))
            {
                if(int.TryParse(rows, out int rowsParsed))
                    MAX_ROWS = rowsParsed;
                else
                    MAX_ROWS = DEFAULT_MAX_ROWS;
            }
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("screenMem", out var screenMem))
            {
                SCREEN_MEMORY_ADDRESS = 0x0200;
                // if(ushort.TryParse(screenMem, out ushort screenMemParsed))
                //     SCREEN_MEMORY_ADDRESS = screenMemParsed;
                // else
                //     SCREEN_MEMORY_ADDRESS = screenMemParsed;
            }
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("colorMem", out var colorMem))
            {
                COLOR_MEMORY_ADDRESS = 0xd800;
                // if(ushort.TryParse(colorMem, out ushort colorMemParsed))
                //     COLOR_MEMORY_ADDRESS = colorMemParsed;
                // else
                //     COLOR_MEMORY_ADDRESS = colorMemParsed;
            }            

            //await _jSRuntime.InvokeAsync<object>("initGame", DotNetObjectReference.Create(this));
            _computer = InitDotNet6502Computer(prgBytes);
            InitEmulatorScreenMemory(_computer);

            // Hack until input from browser: simulate spacebar pressed down
            //_computer.Mem[KEY_DOWN_ADDRESS] = 0x20; // space

            _debugMessages = new List<string>();
            _debugMessages.Add("Starting...");
            ShowDebugMessages = false;

            await base.OnInitializedAsync();
        } 

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender)
                return;

            await _jSRuntime.InvokeAsync<object>("initGame", DotNetObjectReference.Create(this));

            //await jsRuntime.InvokeVoidAsync("setFocusToElement", myReference);  

        }

        [JSInvokable]
        public async ValueTask GameLoop(float timeStamp)
        {
            _frameCounter++;
            if(UPDATE_EVERY_X_FRAME > 0 && (_frameCounter % UPDATE_EVERY_X_FRAME != 0))         
            {
                return;                
            }

            await base.InvokeAsync(() =>
            {
                return;
            });

            HandleEmulatorInput();

            ExecuteEmulator();

            // var stats = $"{_computer.CPU.ExecState.InstructionsExecutionCount} ins, {_computer.CPU.ExecState.CyclesConsumed} cyc";
            // _debugMessages[0] = $"{stats} CPU: {OutputGen.GetProcessorState(_computer.CPU)}";

            this.StateHasChanged();
        }

        protected void OnKeyDown(KeyboardEventArgs e) 
        {
            int keyCode=0;
            if(e.Key.Length==1)
                keyCode = (int)e.Key[0];

            //_debugMessages.Add($"OnKeyDown. Code: {e.Code} Key: {e.Key} KeyCode: {keyCode}");
            InputSystem.Instance.SetKeyState(keyCode ,ButtonState.States.Down);
        }

        protected void OnKeyUp(KeyboardEventArgs e)  
        {
            int keyCode=0;
            if(e.Key.Length==1)
                keyCode = (int)e.Key[0];
                
            //_debugMessages.Add($"OnKeyUp. Code: {e.Code} Key: {e.Key} KeyCode: {keyCode}");
            InputSystem.Instance.SetKeyState(keyCode,ButtonState.States.Up);        
        }

        public void ExecuteEmulator()
        {
            // Execute a number of cycles for one frame
            _computer.CPU.Execute(
                _computer.Mem,
                new LegacyExecEvaluator(
                    new ExecOptions
                    {
                        CyclesRequested = 8000, // TODO: What is max to able to run withing 1/60 second?
                    })
                );

            // Tell CPU 6502 code that one frame worth of CPU cycles has been executed
            SetFrameCompleted();

            // Wait for CPU 6502 code has acknowledged that it knows a frame has completed.
            bool waitOk = WaitFrameCompletedAcknowledged();
            if (!waitOk)
                return;            
        }

        private void SetFrameCompleted()
        {
            _computer.Mem.SetBit(SCREEN_REFRESH_STATUS_ADDRESS, (int)Highbyte.DotNet6502.Systems.Generic.ScreenStatusBitFlags.HostNewFrame);
        }

        private bool WaitFrameCompletedAcknowledged()
        {
            // Keep on executing instructions until CPU 6502 code has cleared bit 0 in ScreenRefreshStatusAddress
            while (_computer.Mem.IsBitSet(SCREEN_REFRESH_STATUS_ADDRESS, (int)Highbyte.DotNet6502.Systems.Generic.ScreenStatusBitFlags.HostNewFrame))
            {
                var execState = _computer.CPU.Execute(
                    _computer.Mem,
                    LegacyExecEvaluator.OneInstructionExecEvaluator);
                // If an unhandled instruction, return false
                if (!execState.LastOpCodeWasHandled)
                    return false;
            }
            return true;
        }


        private void HandleEmulatorInput()
        {
            var keysDown = InputSystem.Instance.GetKeysDown();
            if(keysDown.Length>0)
                _computer.Mem[KEY_DOWN_ADDRESS] = (byte)keysDown[0];
            else
                _computer.Mem[KEY_DOWN_ADDRESS] = 0x00;

             _computer.Mem[RANDOM_VALUE_ADDRESS] = (byte)_rnd.Next(0, 255);
        }

        protected bool EmulatorIsInitialized()
        {
            return _computer!=null;
        }


        protected string GetLayoutRootClass()
        {
            // CSS class for root div element
            return this.ROOT_CSS_CLASS;
        }

        protected string GetFrameClass()
        {
            // CSS class for border div element
            return this.BORDER_CSS_CLASS;
        }


        protected string GetBorderBgColorClass()
        {
            // Common border color
            byte borderColor = this._computer.Mem[BORDER_COLOR_ADDRESS];            
            return C64ColorMap[borderColor].Item2;
        }

        protected string GetBackgroundBgColorClass()
        {
            // Common background color
            byte bgColor = this._computer.Mem[BACKGROUND_COLOR_ADDRESS];            
            return C64ColorMap[bgColor].Item2;
        }


        protected MarkupString GetCharacter(int col, int row)
        {
            var charByte = _computer.Mem[(ushort) (SCREEN_MEMORY_ADDRESS + (row * MAX_COLS) + col)];
            string representAsString;
            switch(charByte)
            {
                case 0x00:  // Uninitialized
                case 0x0a:  // NewLine/CarrigeReturn
                case 0x0d:  // NewLine/CarrigeReturn
                    representAsString = " "; // Replace with space
                    break;
                case 0xa0:  //160, C64 inverted space
                case 0xe0:  //224, Also C64 inverted space?
                    representAsString = @"&#x02588;"; // Unicode for Inverted square in https://style64.org/c64-truetype font
                    break;
                default:
                    representAsString = Convert.ToString((char)charByte);
                    break;
            }
            return new MarkupString(representAsString);
        }

        protected string GetFgColorCssClass(int col, int row)
        {
            byte fgColor = _computer.Mem[(ushort) (COLOR_MEMORY_ADDRESS + (row * MAX_COLS) + col)];
            return C64ColorMap[fgColor].Item1;
        }

        protected string GetBgColorCssClass(int col, int row)
        {
            // Common bg color for all characters on screen
            return GetBackgroundBgColorClass();
        }

        protected IEnumerable<string> GetDebugMessages()
        {
            return _debugMessages;
        }

        private GenericComputer InitDotNet6502Computer(byte[] prgBytes)
        {
            // First two bytes of binary file is assumed to be start address, little endian notation.
            var fileHeaderLoadAddress = ByteHelpers.ToLittleEndianWord(prgBytes[0], prgBytes[1]);
            // The rest of the bytes are considered the code & data
            byte[] codeAndDataActual = new byte[prgBytes.Length-2];
            Array.Copy(prgBytes, 2, codeAndDataActual, 0, prgBytes.Length-2);

            var mem = new Memory();
            mem.StoreData(fileHeaderLoadAddress, codeAndDataActual);

            // Initialize emulator with CPU, memory, and execution parameters
            var computerBuilder = new GenericComputerBuilder(new NullLoggerFactory());
            computerBuilder
                .WithCPU()
                .WithStartAddress(fileHeaderLoadAddress)
                .WithMemory(mem)
                // .WithInstructionExecutedEventHandler( 
                //     (s, e) => System.Diagnostics.Debug.WriteLine(OutputGen.GetLastInstructionDisassembly(e.CPU, e.Mem)))
                .WithExecOptions(options =>
                {
                    options.MaxNumberOfInstructions = 10;
                    options.ExecuteUntilInstruction = OpCodeId.BRK; // Emulator will stop executing when a BRK instruction is reached.
                });
            var computer = computerBuilder.Build();
            return computer;
        }

        private void InitEmulatorScreenMemory(GenericComputer computer)
        {
            var mem = computer.Mem;
            // Common bg and border color for entire screen, controlled by specific address
            mem[BORDER_COLOR_ADDRESS]     = 0x0e;   // light blue
            mem[BACKGROUND_COLOR_ADDRESS] = 0x06;   // blue

            ushort currentScreenAddress = SCREEN_MEMORY_ADDRESS;
            ushort currentColorAddress  = COLOR_MEMORY_ADDRESS;
            for (int row = 0; row < MAX_ROWS; row++)
            {
                for (int col = 0; col < MAX_COLS; col++)
                {
                    mem[currentScreenAddress++] = 0x20;     // 32 (0x20) = space
                    mem[currentColorAddress++] = 0x0e;      // light blue
                }
            }            
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
        static byte[] Base64UrlDecode(string arg)
        {
            string s = arg;
            s = s.Replace('-', '+'); // 62nd char of encoding
            s = s.Replace('_', '/'); // 63rd char of encoding
            switch (s.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 2: s += "=="; break; // Two pad chars
                case 3: s += "="; break; // One pad char
                default: throw new System.Exception(
                "Illegal base64url string!");
            }
            return Convert.FromBase64String(s); // Standard base64 decoder
        }      

    }
}
