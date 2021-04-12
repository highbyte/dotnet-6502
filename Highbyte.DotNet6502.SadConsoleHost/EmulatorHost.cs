using System;
using System.Diagnostics;
using System.IO;

namespace Highbyte.DotNet6502.SadConsoleHost
{
    public class EmulatorHost
    {
        private readonly Options _options;
        private static SadConsoleMain SadConsoleMain;
        
        public EmulatorHost(Options options)
        {
            _options = options;
        }

        public void Start()
        {
            _options.EmulatorConfig.Validate();

            // Init CPU emulator
            var computer = SetupEmulator(_options.EmulatorConfig);

            // Create SadConsole renderer that reads screen data from emulator memory and displays it on a SadConsole screen/console
            var sadConsoleEmulatorRenderer = new SadConsoleEmulatorRenderer(
                GetSadConsoleScreen,
                computer.Mem, 
                _options.EmulatorConfig.Memory.Screen);

            // Init emulator memory based on our configured memory layout
            sadConsoleEmulatorRenderer.InitEmulatorScreenMemory();

            // Create SadConsole input handler that forwards pressed keys to the emulator via memory addresses
            var sadConsoleEmulatorInput = new SadConsoleEmulatorInput(
                computer.Mem, 
                _options.EmulatorConfig.Memory.Input);            

            // Create SadConsole executor that executes instructions in the emulator until a certain memory address has been flagged that emulator code is done for current frame
            var sadConsoleEmulatorExecutor = new SadConsoleEmulatorExecutor(
                computer, 
                _options.EmulatorConfig.Memory.Screen,
                _options.EmulatorConfig.StopAtBRK);

            // Create the main game loop class that invokes emulator and render to host screen
            var sadConsoleEmulatorLoop = new SadConsoleEmulatorLoop(
                sadConsoleEmulatorRenderer, 
                sadConsoleEmulatorInput,
                sadConsoleEmulatorExecutor,
                updateEmulatorEveryXFrame: _options.EmulatorConfig.RunEmulatorEveryFrame);

            // Create the main SadConsole class that is responsible for configuring and starting up SadConsole with our preferred configuration.
            SadConsoleMain = new SadConsoleMain(
                _options.SadConsoleConfig,
                _options.EmulatorConfig.Memory.Screen,
                sadConsoleEmulatorLoop);
 
            // Start SadConsole. Will exit from this method after SadConsole window is closed.
            SadConsoleMain.Run();
        }

        private SadConsoleScreen GetSadConsoleScreen()
        {
            return SadConsoleMain.SadConsoleScreen;
        }
        
        private Computer SetupEmulator(EmulatorConfig emulatorConfig)
        {
            Debug.WriteLine($"Loading 6502 machine code binary file.");
            Debug.WriteLine($"{emulatorConfig.ProgramBinaryFile}");
            if(!File.Exists(emulatorConfig.ProgramBinaryFile))
            {
                Debug.WriteLine($"File does not exist.");
                throw new Exception($"Cannot find 6502 binary file: {emulatorConfig.ProgramBinaryFile}");
            }

            var enableBankSwitching = emulatorConfig.Memory.MemoryBanks.EnableMemoryBanks;
            var mem = new Memory(enableBankSwitching: enableBankSwitching);
            if(enableBankSwitching)
            {
                // Add additional memory banks for memory segment 1 (0x2000) and up (segment 0 cannot have multiple banks)
                for (byte memorySegmentNumber = 1; memorySegmentNumber < mem.MemorySegments.Count; memorySegmentNumber++)
                {
                    // By default each segment has one bank when Memory is created above.
                    // Thus we add the specified BanksPerSegment-1 new banks to each segment.
                    for (int i = 0; i < emulatorConfig.Memory.MemoryBanks.BanksPerSegment-1; i++)
                    {
                        // Add additional memory banks for segment. Memory in those will be blank (0x00).
                        mem.AddMemorySegmentBank(memorySegmentNumber);
                    }
                }
            }

            BinaryLoader.Load(
                mem,
                emulatorConfig.ProgramBinaryFile, 
                out ushort loadedAtAddress, 
                out int fileLength);

            // Initialize emulator with CPU, memory, and execution parameters
            var computerBuilder = new ComputerBuilder();
            computerBuilder
                .WithCPU()
                .WithStartAddress(loadedAtAddress)
                .WithMemory(mem)
                .WithExecOptions(options =>
                {
                    // Emulator will stop executing when a BRK instruction is reached.
                    options.ExecuteUntilInstruction = emulatorConfig.StopAtBRK?OpCodeId.BRK:null; 
                });

            var computer = computerBuilder.Build();
            return computer;
        }
    }
}