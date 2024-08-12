using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests;

[Trait("TestType", "Integration")]
public class Functional_BCD_test
{
    private readonly ITestOutputHelper _output;
    public Functional_BCD_test(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    /// <summary>
    /// This test runs a 6502 functional test program that tests the BCD mode of the 6502.
    /// The source code is from 
    /// http://www.6502.org/tutorials/decimal_mode.html
    /// and it has been pre-assembled to a binary file in this repo (test_bcd_mode.prg)
    /// </summary>
    [Fact]
    public void Can_Run_6502_Functional_Test_Program_With_Decimal_Mode_Enabled_Successfully()
    {
        // Arrange

        var functionalTestBinary = @"../../../../../samples/Assembler/Generic/Build/test_bcd_mode.prg";

        // There is a 2 byte header in the test_bcd_mode.prg file.
        // Assume it's specified to 0xc000.
        ushort loadAddress = 0xc000;

        var mem = BinaryLoader.Load(
            functionalTestBinary,
            out ushort loadedAtAddress,
            out ushort fileLength);

        Assert.Equal(loadAddress, loadedAtAddress);

        _output.WriteLine($"Data & code load  address: {loadAddress.ToHex(),10} ({loadAddress})");
        _output.WriteLine($"Code+data length (bytes):  0x{fileLength,-8:X8} ({fileLength})");
        _output.WriteLine($"Code start address:        {loadAddress.ToHex(),10} ({loadAddress})");

        var cpu = new CPU();
        cpu.PC = loadedAtAddress;

        var maxInstructions = 50000000;
        ushort executeUntilExecutedInstructionAtPC = (ushort)(loadAddress + 0x005a);    // RTS
        ushort errorAddress = (ushort)(loadAddress + 0x01b0);                           // The ERROR label in the assembly source code

        _output.WriteLine($"The test program will reach a specific memory location when it's done (either success or failure): {executeUntilExecutedInstructionAtPC.ToHex()}, and the emulator will then stop processing.");
        _output.WriteLine($"The error code of the run will be stored in address {errorAddress.ToHex()}");
        _output.WriteLine($"- 0 = success");
        _output.WriteLine($"- 1 = fail");

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
        _output.WriteLine($"Done.");

        // Assert
        var execState = cpu.ExecState;
        _output.WriteLine($"CPU last PC:                       {cpu.PC.ToHex()}");
        _output.WriteLine($"CPU last opcode:                   {execState.LastInstructionExecResult.OpCodeByte.ToOpCodeId()} ({execState.LastInstructionExecResult.OpCodeByte.ToHex()})");
        _output.WriteLine($"Total # CPU instructions executed: {insCount}");
        _output.WriteLine($"Total # CPU cycles consumed:       {execState.CyclesConsumed}");

        var error = mem[errorAddress];
        _output.WriteLine($"Error value:                       {error}");

        if (error == 0)
        {
            _output.WriteLine($"Success.");
        }
        else
        {
            _output.WriteLine($"Failure.");
            _output.WriteLine("One of the tested BCD calculations gave unexpected result");
            _output.WriteLine("Or the C# unit test code is not configured to run for enough cycles to complete.");
        }
        Assert.Equal(0, error);
    }
}
