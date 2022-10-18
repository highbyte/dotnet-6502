using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Generic;

namespace BlazorWasmSkiaTest.Helpers
{
    public class EmulatorHelper
    {
        private GenericComputer? _computer;
        private readonly Random _rnd = new();

        private readonly ushort _screenMemoryAddress = DEFAULT_SCREEN_MEMORY_ADDRESS;
        private readonly ushort _colorMemoryAddress = DEFAULT_COLOR_MEMORY_ADDRESS;
        private readonly ushort _borderColorAddress = DEFAULT_BORDER_COLOR_ADDRESS;
        private readonly ushort _backgroundColorAddress = DEFAULT_BACKGROUND_COLOR_ADDRESS;

        public int MaxCols { get; private set; } = DEFAULT_MAX_COLS;
        public int MaxRows { get; private set; } = DEFAULT_MAX_ROWS;

        private enum ScreenStatusBitFlags : int
        {
            HostNewFrame = 0,
            EmulatorDoneForFrame = 1,
        }

        private const int DEFAULT_MAX_COLS = 40;
        private const int DEFAULT_MAX_ROWS = 25;
        private const ushort DEFAULT_SCREEN_MEMORY_ADDRESS = 0x0400;
        private const ushort DEFAULT_COLOR_MEMORY_ADDRESS = 0xd800;
        private const ushort DEFAULT_BORDER_COLOR_ADDRESS = 0xd020;
        private const ushort DEFAULT_BACKGROUND_COLOR_ADDRESS = 0xd021;

        // Memory address in emulator that the 6502 program and the host will use to communicate if current frame is done or not.
        private const ushort SCREEN_REFRESH_STATUS_ADDRESS = 0xd000;
        // Currently pressed key on host (ASCII byte). If no key is pressed, value is 0x00
        private const ushort KEY_PRESSED_ADDRESS = 0xd030;
        // Currently down key on host (ASCII byte). If no key is down, value is 0x00
        private const ushort KEY_DOWN_ADDRESS = 0xd031;
        // Currently released key on host (ASCII byte). If no key is down, value is 0x00
        private const ushort KEY_RELEASED_ADDRESS = 0xd031;

        // Memory address to store a randomly generated value between 0-255
        private const ushort RANDOM_VALUE_ADDRESS = 0xd41b;

        public EmulatorHelper(int? cols, int? rows, ushort? screenMemoryAddress, ushort? colorMemoryAddress)
        {
            if (cols.HasValue)
                MaxCols = cols.Value;
            if (rows.HasValue)
                MaxRows = rows.Value;
            if (screenMemoryAddress.HasValue)
                _screenMemoryAddress = screenMemoryAddress.Value;
            if (colorMemoryAddress.HasValue)
                _colorMemoryAddress = colorMemoryAddress.Value;
        }

        public void InitDotNet6502Computer(byte[] prgBytes)
        {
            // First two bytes of binary file is assumed to be start address, little endian notation.
            var fileHeaderLoadAddress = ByteHelpers.ToLittleEndianWord(prgBytes[0], prgBytes[1]);
            // The rest of the bytes are considered the code & data
            var codeAndDataActual = new byte[prgBytes.Length - 2];
            Array.Copy(prgBytes, 2, codeAndDataActual, 0, prgBytes.Length - 2);

            var mem = new Memory();
            mem.StoreData(fileHeaderLoadAddress, codeAndDataActual);

            // Initialize emulator with CPU, memory, and execution parameters
            var computerBuilder = new GenericComputerBuilder();
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
            _computer = computerBuilder.Build();

            InitEmulatorScreenMemory();
        }

        private void InitEmulatorScreenMemory()
        {
            var mem = _computer!.Mem;
            // Common bg and border color for entire screen, controlled by specific address
            mem[_borderColorAddress] = 0x0e;   // light blue
            mem[_backgroundColorAddress] = 0x06;   // blue

            var currentScreenAddress = _screenMemoryAddress;
            var currentColorAddress = _colorMemoryAddress;
            for (var row = 0; row < MaxRows; row++)
            {
                for (var col = 0; col < MaxCols; col++)
                {
                    mem[currentScreenAddress++] = 0x20;     // 32 (0x20) = space
                    mem[currentColorAddress++] = 0x0e;      // light blue
                }
            }
        }

        public void ExecuteEmulator()
        {
            AssertComputerInitialized();

            // Set emulator Refresh bit
            // Emulator will wait until this bit is set until "redrawing" new data into memory
            _computer!.Mem.SetBit(SCREEN_REFRESH_STATUS_ADDRESS, (int)ScreenStatusBitFlags.HostNewFrame);

            var shouldExecuteEmulator = true;
            while (shouldExecuteEmulator)
            {
                // Execute a number of instructions
                // TODO: _computer there a more optimal number of instructions to execute before we check if emulator code has flagged it's done via memory flag?
                _computer.Run(LegacyExecEvaluator.InstructionCountExecEvaluator(10)); // TODO: What is the optimal number of cycles to execute in each loop?
                shouldExecuteEmulator = !_computer.Mem.IsBitSet(SCREEN_REFRESH_STATUS_ADDRESS, (int)ScreenStatusBitFlags.EmulatorDoneForFrame);
            }

            // Clear the flag that the emulator set to indicate it's done.
            _computer.Mem.ClearBit(SCREEN_REFRESH_STATUS_ADDRESS, (int)ScreenStatusBitFlags.EmulatorDoneForFrame);
        }

        public void KeyDown(int keyCode)
        {
            AssertComputerInitialized();
            _computer!.Mem[KEY_DOWN_ADDRESS] = (byte)keyCode;
        }

        public void KeyUp(int keyCode)
        {
            AssertComputerInitialized();
            _computer!.Mem[KEY_DOWN_ADDRESS] = 0x00;
        }

        public void GenerateRandomNumber()
        {
            AssertComputerInitialized();
            _computer!.Mem[RANDOM_VALUE_ADDRESS] = (byte)_rnd.Next(0, 255);
        }

        public byte GetScreenCharacter(int col, int row)
        {
            AssertComputerInitialized();
            return _computer!.Mem[(ushort)(_screenMemoryAddress + row * MaxCols + col)];
        }

        public byte GetScreenCharacterForegroundColor(int col, int row)
        {
            AssertComputerInitialized();
            return _computer!.Mem[(ushort)(_colorMemoryAddress + row * MaxCols + col)];
        }

        public byte GetBackgroundColor()
        {
            AssertComputerInitialized();
            return _computer!.Mem[_backgroundColorAddress];
        }

        public byte GetBorderColor()
        {
            AssertComputerInitialized();
            return _computer!.Mem[_borderColorAddress];
        }

        private void AssertComputerInitialized()
        {
            if (_computer == null)
                throw new InvalidOperationException($"Computer object not initialized. Call {nameof(InitDotNet6502Computer)}");
        }
    }
}
