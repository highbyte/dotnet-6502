using Xunit;
using Xunit.Abstractions;
using Highbyte.DotNet6502.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests
{
    [Trait("TestType", "Integration")] 
    public class Functional_test
    {
        private readonly ITestOutputHelper _output;
        public Functional_test(ITestOutputHelper testOutputHelper)
        {
            _output = testOutputHelper;
        }

        [Fact]
        public void Can_Run_6502_Functional_Test_Program_With_Decimal_Mode_Disabled_Successfully()
        {
            // Arrange
            var functionalTestCompiler = new FunctionalTestCompiler(NullLogger<FunctionalTestCompiler>.Instance);
            var functionalTestBinary = functionalTestCompiler.Get6502FunctionalTestBinary(disableDecimalTests: true);

            // There is no 2 byte header in the 6502_functional_test.bin file.
            // It's supposed to be loaded to memory at 0x0000, and started at 0x0400
            ushort loadAddress  = 0x000A;
            ushort startAddress = 0x0400;

            var mem = BinaryLoader.Load(
                functionalTestBinary, 
                out ushort loadedAtAddress, 
                out ushort fileLength,
                forceLoadAddress: loadAddress);

            _output.WriteLine($"Data & code load  address: {loadAddress.ToHex(), 10} ({loadAddress})");
            _output.WriteLine($"Code+data length (bytes):  0x{fileLength, -8:X8} ({fileLength})");
            _output.WriteLine($"Code start address:        {startAddress.ToHex(), 10} ({startAddress})");

            var cpu = new CPU();
            cpu.PC = startAddress;

            var maxInstructions = 50000000;
            ushort executeUntilExecutedInstructionAtPC = 0x336d;

            _output.WriteLine($"If test logic succeeds, the test program will reach a specific memory location: {executeUntilExecutedInstructionAtPC.ToHex()}, and the emulator will then stop processing.");
            _output.WriteLine($"If test logic fails, the test program will loop forever at the location the error was found. The emulator will try executing a maximum #instructions {executeUntilExecutedInstructionAtPC} before giving up.");
            _output.WriteLine($"If unknown opcode is found, it's logged and ignored, and processing continues on next instruction.");

            // Act
            int insCount = 0;
            while (true)
            {
                insCount++;
                cpu.ExecuteOneInstruction(mem);
                if (cpu.PC == executeUntilExecutedInstructionAtPC)
                    break;
                if (insCount >= maxInstructions)
                    break;
            }

            // Assert
            var execState = cpu.ExecState;

            _output.WriteLine($"CPU last PC:                       {cpu.PC.ToHex()}");
            _output.WriteLine($"CPU last opcode:                   {execState.LastInstructionExecResult.OpCodeByte.ToOpCodeId()} ({execState.LastInstructionExecResult.OpCodeByte.ToHex()})");
            _output.WriteLine($"Total # CPU instructions executed: {execState.InstructionsExecutionCount}");
            _output.WriteLine($"Total # CPU cycles consumed:       {execState.CyclesConsumed}");

            if (cpu.PC == executeUntilExecutedInstructionAtPC)
            {
                _output.WriteLine($"Success. PC reached expected success memory location: {executeUntilExecutedInstructionAtPC.ToHex()}");
                Assert.True(true);
            }
            else
            {
                _output.WriteLine($"Probably failure. The emulator executer a maximum #instructions {maxInstructions}, and did not manage to get PC to the configured success location: {executeUntilExecutedInstructionAtPC.ToHex()}");
                _output.WriteLine($"The functional test program would end in a forever-loop on the same memory location if it fails.");
                _output.WriteLine($"Verify the last PC location against the functional test program's .lst file to find out which logic test failed.");
                Assert.True(false);
            }
        }
    }
}
