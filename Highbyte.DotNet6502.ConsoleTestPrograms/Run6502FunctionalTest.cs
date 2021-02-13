using System;
using Highbyte.DotNet6502.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.ConsoleTestPrograms
{
    public class Run6502FunctionalTest
    {
        //readonly ILogger<Run6502FunctionalTest> _logger;
        readonly FunctionalTestCompiler _functionalTestCompiler;
        public Run6502FunctionalTest(
            //ILogger<Run6502FunctionalTest> logger,
            FunctionalTestCompiler functionalTestCompiler)
        {
            //_logger = logger;
            _functionalTestCompiler = functionalTestCompiler;
        }

        public void Run()
        {
            Console.WriteLine($"------------------------------------------------------------------");
            Console.WriteLine($"Run 6502 functional test program with decimal tests disabled");
            Console.WriteLine("Functional test downloaded and compiled from:");
            Console.WriteLine($"------------------------------------------------------------------");

            Console.WriteLine($"Downloading functional test source code from");
            Console.WriteLine("https://github.com/Klaus2m5/6502_65C02_functional_tests");
            Console.WriteLine($"Modifying it to set source code line 'disable_decimal = 0' to 'disable_decimal = 1'");
            Console.WriteLine($"And compiling it to a binary that can be loaded into the 6502 emulator...");

            // Set the download directory to the same directory where current application runs in.
            var currentAssemblyLocation = System.Reflection.Assembly.GetEntryAssembly().Location;
            var downloadDir = System.IO.Path.GetDirectoryName(currentAssemblyLocation);

            var functionalTestBinary = _functionalTestCompiler.Get6502FunctionalTestBinary(
                disableDecimalTests: true,
                downloadDir: downloadDir
                );

            Console.WriteLine($"Download and compilation complete.");
            Console.WriteLine($"Binary location (as well as .lst file):");
            Console.WriteLine($"{functionalTestBinary}");

            // There is no 2 byte header in the 6502_functional_test.bin file.
            // It's supposed to be loaded to memory at 0x0000, and started at 0x0400
            Console.WriteLine("");
            Console.WriteLine($"Loading binary into emulator memory...");
            ushort loadAddress  = 0x000A;
            ushort startAddress = 0x0400;

            var mem = BinaryLoader.Load(
                functionalTestBinary, 
                out ushort loadedAtAddress, 
                out int fileLength,
                forceLoadAddress: loadAddress);
            Console.WriteLine($"Loading done.");

            // The rest of the bytes are considered the code
            Console.WriteLine("");
            Console.WriteLine($"Data & code load address:  {loadAddress.ToHex(), 10} ({loadAddress})");
            Console.WriteLine($"Code+data length (bytes):  0x{fileLength, -8:X8} ({fileLength})");
            Console.WriteLine($"Code start address:        {startAddress.ToHex(), 10} ({startAddress})");

            //Console.WriteLine("Press Enter to start");
            //Console.ReadLine();

            // Initialize CPU, set PC to start position
            var computerBuilder = new ComputerBuilder();
            computerBuilder
                .WithCPU()
                .WithStartAddress(0x400)
                .WithMemory(mem)
                //.WithInstructionAboutToBeExecutedEventHandler(OnInstructionToBeExecuted)
                .WithInstructionExecutedEventHandler(OnInstructionExecuted)
                .WithUnknownInstructionEventHandler(OnUnknownOpCodeDetected)
                .WithExecOptions(options =>
                {
                    options.ExecuteUntilExecutedInstructionAtPC = 0x336d; 
                    // A successful run has about 26765880 instructions (the version that was run 2021-02-06, that may change)
                    // We increase to almost double, and will exit if not finished then.
                    options.MaxNumberOfInstructions = 50000000; 
                    options.UnknownInstructionThrowsException = false; 
                });

            var computer = computerBuilder.Build();

            Console.WriteLine("");
            Console.WriteLine($"If test logic succeeds, the test program will reach a specific memory location: {computer.ExecOptions.ExecuteUntilExecutedInstructionAtPC.Value.ToHex()}, and the emulator will then stop processing.");
            Console.WriteLine($"If test logic fails, the test program will loop forever at the location the error was found. The emulator will try executing a maximum #instructions {computer.ExecOptions.MaxNumberOfInstructions.Value} before giving up.");
            Console.WriteLine($"If unknown opcode is found, it's logged and ignored, and processing continues on next instruction.");

            // Execute program
            Console.WriteLine("");
            Console.WriteLine("Starting code execution...");

            computer.Run();

            Console.WriteLine("");
            Console.WriteLine("Code execution done.");

            var cpu = computer.CPU;
            var execState = cpu.ExecState;
            Console.WriteLine("");
            Console.WriteLine($"CPU last PC:                       {cpu.PC.ToHex()}");
            Console.WriteLine($"CPU last opcode:                   {execState.LastOpCode.Value.ToOpCodeId()} ({execState.LastOpCode.Value.ToHex()})");
            Console.WriteLine($"Total # CPU instructions executed: {execState.InstructionsExecutionCount}");
            Console.WriteLine($"Total # CPU cycles consumed:       {execState.CyclesConsumed}");

            Console.WriteLine("");
            // Evaluate success/failure
            if(cpu.PC == computer.ExecOptions.ExecuteUntilExecutedInstructionAtPC.Value)
            {
                Console.WriteLine($"Success!");
                Console.WriteLine($"PC reached expected success memory location: {computer.ExecOptions.ExecuteUntilExecutedInstructionAtPC.Value.ToHex()}");
            }
            else
            {
                Console.WriteLine($"Probably failure");
                Console.WriteLine($"The emulator executer a maximum #instructions {computer.ExecOptions.MaxNumberOfInstructions.Value}, and did not manage to get PC to the configured success location: {computer.ExecOptions.ExecuteUntilExecutedInstructionAtPC.Value.ToHex()}");
                Console.WriteLine($"The functional test program would end in a forever-loop on the same memory location if it fails.");
                Console.WriteLine($"Verify the last PC location against the functional test program's .lst file to find out which logic test failed.");
            }

        }

        void OnInstructionExecuted(object sender, CPUInstructionExecutedEventArgs e)
        {
            const int showEveryXInstructions = 2000000;
            if(e.CPU.ExecState.InstructionsExecutionCount % showEveryXInstructions == 0)
                Console.WriteLine($"{OutputGen.FormatLastInstruction(e.CPU, e.Mem)}  (ins. count: {e.CPU.ExecState.InstructionsExecutionCount})");
        }

        void OnUnknownOpCodeDetected(object sender, CPUUnknownOpCodeDetectedEventArgs e)
        {
            const int showEveryXUnknownInstruction = 1;
            if(e.CPU.ExecState.UnknownOpCodeCount % showEveryXUnknownInstruction == 0)
                Console.WriteLine($"{e.CPU.ExecState.PCBeforeLastOpCodeExecuted.Value.ToHex()}: {e.OpCode.ToHex()} !!! Unknown opcode !!!");
        }          
    }
}
