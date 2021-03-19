using Microsoft.AspNetCore.Components;
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

        [Parameter]
        public List<string> DisplayRows  { get; set; }

        [Inject]
        protected IJSRuntime _jSRuntime {get; set;}

        [Inject]
        protected HttpClient _httpClient {get; set;}

        static Dictionary<byte, string> C64ColorMap = new()
        {
            { 0x00, "rgb(0, 0, 0)"},          // Black
            { 0x01, "rgb(255, 255, 255)"},    // White
            { 0x02, "rgb(136, 0, 0)"},        // Red
            { 0x03, "rgb(170, 255, 238)"},    // Cyan
            { 0x04, "rgb(204, 68, 204)"},     // Violet/purple
            { 0x05, "rgb(0, 204, 85)"},       // Green
            { 0x06, "rgb(0, 0, 170)"},        // Blue
            { 0x07, "rgb(238, 238, 119)"},    // Yellow
            { 0x08, "rgb(221, 136, 185)"},    // Orange
            { 0x09, "rgb(102, 68, 0)"},       // Brown
            { 0x0a, "rgb(255, 119, 119)"},    // Light red
            { 0x0b, "rgb(51, 51, 51)"},       // Dark grey
            { 0x0c, "rgb(119, 119, 119)"},    // Grey
            { 0x0d, "rgb(170, 255, 102)"},    // Light green
            { 0x0e, "rgb(0, 136, 255)"},      // Light blue
            { 0x0f, "rgb(187, 187, 187)"},    // Light grey
        };

        private ulong _frameCounter = 0;
        const int UPDATE_EVERY_X_FRAME = 0;

        const string PRG_URL = "6502binaries/hostinteraction_scroll_text_and_cycle_colors.prg";

        const int MAX_COLS = 40;
        const int MAX_ROWS = 25;

        private Computer _computer;

        private string _debugMessage;

        public DotNet6502EmulatorComponent()
        {

            DisplayRows = new List<string>();
            string emptyRow = new(' ', MAX_COLS);
            for (int i = 0; i < MAX_ROWS; i++)
            {
                DisplayRows.Add(emptyRow);
            }
        }
        protected override async Task OnInitializedAsync()
        {
            //await _jSRuntime.InvokeAsync<object>("initGame", DotNetObjectReference.Create(this));
            _computer = await InitDotNet6502Computer(PRG_URL);

            await base.OnInitializedAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender)
                return;

            await _jSRuntime.InvokeAsync<object>("initGame", DotNetObjectReference.Create(this));

        }

        [JSInvokable]
        public async ValueTask GameLoop(float timeStamp)
        {
            _frameCounter++;
            if(UPDATE_EVERY_X_FRAME > 0 && (_frameCounter % UPDATE_EVERY_X_FRAME != 0))         
            {
                return;                
            }

            DisplayRows[1] = $"{_frameCounter}";
            await base.InvokeAsync(() =>
            {
                return;
            });

            ExecuteEmulator();
            var stats = $"{_computer.CPU.ExecState.InstructionsExecutionCount} ins, {_computer.CPU.ExecState.CyclesConsumed} cyc";
            _debugMessage = $"{stats} CPU: {OutputGen.GetProcessorState(_computer.CPU)}";

            //DisplayTestFrame();

            DisplayScreenMemory();

            //DisplayDebugMessage();

            this.StateHasChanged();

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

        private void DisplayScreenMemory()
        {
            // TODO: Have common bg color like C64 or allow separate bg color per character in another memory range?
            byte bgColor = this._computer.Mem[0xd021];

            // Build screen data characters based on emulator memory contents (byte)
            ushort currentScreenAddress = 0x0400;
            ushort currentColorAddress = 0xd800;
            for (int row = 0; row < MAX_ROWS; row++)
            {
                byte[] byteArray = new byte[MAX_COLS];
                for (int col = 0; col < MAX_COLS; col++)
                {
                    var chrByte = _computer.Mem[currentScreenAddress++];
                    // TODO: Remove hack to make value 0x00 mean space (0x20) if we haven't initialized screen memory.
                    if(chrByte==0x00)
                        chrByte=0x20;
                    // if(chrByte==0x20)
                    //     chrByte=0x2e;   // Temporary show period where every space is
                    byteArray[col] = chrByte;
                }
                DisplayRows[row] = Encoding.UTF8.GetString(byteArray);    

                // for (int col = 0; col < MAX_COLS; col++)
                // {
                //     byte charByte = _computer.Mem[currentScreenAddress++];
                //     byte colorByte = _computer.Mem[currentColorAddress++];
                //     DrawEmulatorCharacterOnScreen(
                //         col, 
                //         row,
                //         charByte, 
                //         colorByte, 
                //         bgColor);
                // }

            }          
        }

        private void DrawEmulatorCharacterOnScreen(int col, int row, byte charByte, byte colorByte, byte bgColor)
        {
        }

        private void DisplayTestFrame()
        {
            for (int col = 0; col < MAX_COLS; col++)
            {
                for (int row = 0; row < MAX_ROWS; row++)
                {
                    if(col==0 || row == 0 || col == (MAX_COLS-1) || row == (MAX_ROWS-1))
                    {
                        byte chrAsc;
                        if(row==0)
                            chrAsc =(byte)(col%10);
                        else if (col == 0)
                            chrAsc =(byte)(row%10);
                        else
                            chrAsc = (byte)'*';
                    DrawEmulatorCharacterOnScreen(
                        col, 
                        row,
                        chrAsc, 
                        0x0f, 
                        0x06);
                    }               
                }
            }            
        }

        private void DisplayDebugMessage()
        {
            int col=0;
            int row=MAX_ROWS -1;
            foreach (char chr in _debugMessage)
            {
                if(col>=MAX_COLS)
                    break;
                DrawEmulatorCharacterOnScreen(
                    col++, 
                    row,
                    (byte)chr, 
                    0x0f, 
                    0x00);              
            }
        }

        private async Task<Computer> InitDotNet6502Computer(string prgDownloadUrl)
        {
            var prgBytes = await _httpClient.GetByteArrayAsync(prgDownloadUrl);
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
    }
}