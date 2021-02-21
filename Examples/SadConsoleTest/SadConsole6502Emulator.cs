using System;
using System.Diagnostics;
using System.IO;
using Highbyte.DotNet6502;

namespace SadConsoleTest
{
    public class SadConsole6502Emulator
    {
        private static SadConsoleMain SadConsoleMain;
        
        public void Start(
            string prgFileName,
            EmulatorMemoryConfig emulatorMemoryConfig,
            SadConsoleConfig sadConsoleConfig)
        {
            // Check for incorrect memory config, overlapping addresses, etc.
            emulatorMemoryConfig.Validate();

            // Init CPU emulator
            var computer = SetupEmulator(prgFileName);

            // Create SadConsole renderer that reads screen data from emulator memory and displays it on a SadConsole screen/console
            var sadConsoleEmulatorRenderer = new SadConsoleEmulatorRenderer(
                GetSadConsoleScreen,
                computer.Mem, 
                emulatorMemoryConfig.EmulatorScreenConfig);

            // Init emulator memory based on our configured memory layout
            sadConsoleEmulatorRenderer.InitEmulatorScreenMemory();

            // Create SadConsole input handler that forwards pressed keys to the emulator via memory addresses
            var sadConsoleEmulatorInput = new SadConsoleEmulatorInput(
                computer.Mem, 
                emulatorMemoryConfig.EmulatorInputConfig);            

            // Create SadConsole executor that executes instructions in the emulator until a certain memory address has been flagged that emulator code is done for current frame
            var sadConsoleEmulatorExecutor = new SadConsoleEmulatorExecutor(
                computer, 
                emulatorMemoryConfig.EmulatorScreenConfig);            

            // Create the main game loop class that invokes emulator and render to host screen
            var sadConsoleEmulatorLoop = new SadConsoleEmulatorLoop(
                sadConsoleEmulatorRenderer, 
                sadConsoleEmulatorInput,
                sadConsoleEmulatorExecutor,
                updateEmulatorEveryXFrame: 1);

            // Create the main SadConsole class that is responsible for configuring and starting up SadConsole with our preferred configuration.
            SadConsoleMain = new SadConsoleMain(
                sadConsoleConfig,
                emulatorMemoryConfig.EmulatorScreenConfig,
                sadConsoleEmulatorLoop);
 
            // Start SadConsole. Will exit from this method after SadConsole window is closed.
            SadConsoleMain.Run();
        }

        private SadConsoleScreen GetSadConsoleScreen()
        {
            return SadConsoleMain.SadConsoleScreen;
        }
        
        private Computer SetupEmulator(string prgFileName)
        {
            Debug.WriteLine($"Loading 6502 machine code binary file.");
            Debug.WriteLine($"{prgFileName}");
            if(!File.Exists(prgFileName))
            {
                Debug.WriteLine($"File does not exist.");
                throw new Exception($"Cannot find 6502 binary file: {prgFileName}");
            }

            var mem = BinaryLoader.Load(
                prgFileName, 
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
                    options.ExecuteUntilInstruction = OpCodeId.BRK; // Emulator will stop executing when a BRK instruction is reached.
                });

            var computer = computerBuilder.Build();
            return computer;
        }
    }
}