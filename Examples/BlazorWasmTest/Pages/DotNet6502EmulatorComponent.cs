using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Highbyte.DotNet6502;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using BlazorWasmTest.Helpers;



namespace BlazorWasmTest
{
    public class DotNet6502EmulatorComponent : ComponentBase
    {
        [Inject]
        protected IJSRuntime _jSRuntime {get; set;}

        [Inject]
        protected HttpClient _httpClient {get; set;}

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

        const string PRG_URL = "6502binaries/hostinteraction_scroll_text_and_cycle_colors.prg";

        protected const int MAX_COLS = 40;
        protected const int MAX_ROWS = 25;

        const ushort SCREEN_MEMORY_ADDRESS = 0x0400;
        const ushort COLOR_MEMORY_ADDRESS  = 0xd800;
        const ushort BORDER_COLOR_ADDRESS = 0xd020; 
        const ushort BACKGROUND_COLOR_ADDRESS = 0xd021;

        // Currently pressed key on host (ASCII byte). If no key is pressed, value is 0x00
        const ushort KEY_PRESSED_ADDRESS = 0xd030;
        // Currently down key on host (ASCII byte). If no key is down, value is 0x00
        const ushort KEY_DOWN_ADDRESS = 0xd031;
        // Currently released key on host (ASCII byte). If no key is down, value is 0x00
        const ushort KEY_RELEASED_ADDRESS = 0xd031;

        private Computer _computer = null;

        protected bool ShowDebugMessages = false;
        private List<string> _debugMessages;

        protected override async Task OnInitializedAsync()
        {
            //await _jSRuntime.InvokeAsync<object>("initGame", DotNetObjectReference.Create(this));
            var prgBytes = await _httpClient.GetByteArrayAsync(PRG_URL);
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
            // Set emulator Refresh bit
            // Emulator will wait until this bit is set until "redrawing" new data into memory
            _computer.Mem.SetBit(0xd000 , (int)ScreenStatusBitFlags.HostNewFrame);

            bool shouldExecuteEmulator = true;
            while(shouldExecuteEmulator)
            {
                // Execute a number of instructions
                // TODO: _computer there a more optimal number of instructions to execute before we check if emulator code has flagged it's done via memory flag?
                _computer.Run(new ExecOptions{MaxNumberOfInstructions = 10}); // TODO: What is the optimal number of cycles to execute in each loop?
                shouldExecuteEmulator = !_computer.Mem.IsBitSet(0xd000, (int)ScreenStatusBitFlags.EmulatorDoneForFrame);
            }
            
            // Clear the flag that the emulator set to indicate it's done.
            _computer.Mem.ClearBit(0xd000, (int)ScreenStatusBitFlags.EmulatorDoneForFrame);
        }

        private void HandleEmulatorInput()
        {
            var keysDown = InputSystem.Instance.GetKeysDown();
            if(keysDown.Length>0)
                _computer.Mem[KEY_DOWN_ADDRESS] = (byte)keysDown[0];
            else
                _computer.Mem[KEY_DOWN_ADDRESS] = 0x00;
        }

        protected bool EmulatorIsInitialized()
        {
            return _computer!=null;
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


        protected char GetCharacter(int col, int row)
        {
            var charByte = _computer.Mem[(ushort) (SCREEN_MEMORY_ADDRESS + (row * MAX_COLS) + col)];
            if(charByte==0x00)
                charByte=0x020; // space
            return  (char)charByte;
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
        

        private Computer InitDotNet6502Computer(byte[] prgBytes)
        {
            // First two bytes of binary file is assumed to be start address, little endian notation.
            var fileHeaderLoadAddress = ByteHelpers.ToLittleEndianWord(prgBytes[0], prgBytes[1]);
            // The rest of the bytes are considered the code & data
            byte[] codeAndDataActual = new byte[prgBytes.Length-2];
            Array.Copy(prgBytes, 2, codeAndDataActual, 0, prgBytes.Length-2);

            var mem = new Memory();
            mem.StoreData(fileHeaderLoadAddress, codeAndDataActual);

            // Initialize emulator with CPU, memory, and execution parameters
            var computerBuilder = new ComputerBuilder();
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

        private void InitEmulatorScreenMemory(Computer computer)
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

    }
}