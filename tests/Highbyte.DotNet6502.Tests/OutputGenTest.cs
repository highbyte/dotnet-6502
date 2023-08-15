namespace Highbyte.DotNet6502.Tests;

public class OutputGenTest
{
    [Fact]
    public void OutputGen_Returns_Correctly_Formatted_Disassembly_If_OpCode_Is_Known()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();
        mem[0x1000] = OpCodeId.LDX_I.ToByte();
        mem[0x1001] = 0xee;

        // Act
        var outputString = OutputGen.GetInstructionDisassembly(cpu, mem, 0x1000);

        // Assert
        Assert.Equal("1000  a2 ee     LDX #$EE   ", outputString);
    }

    [Fact]
    public void OutputGen_Returns_Correctly_Formatted_Disassembly_If_OpCode_Is_Unknown()
    {
        // Arrange
        var cpu = new CPU();
        var mem = new Memory();
        mem[0x1000] = 0xff; // 0xff is not a (official) 6502 opcode

        // Act
        var outputString = OutputGen.GetInstructionDisassembly(cpu, mem, 0x1000);

        // Assert
        Assert.Equal("1000  ff        ???        ", outputString);
    }

    [Fact]
    public void OutputGen_Returns_Correctly_Formatted_Disassembly_For_Last_Executed_Instruction()
    {
        // Arrange
        var instructionExecutionResult = InstructionExecResult.KnownInstructionResult(OpCodeId.LDX_I.ToByte(), 0xc0a0, 0);
        var cpu = new CPU(ExecState.ExecStateAfterInstruction(instructionExecutionResult));
        var mem = new Memory();
        mem[0xc0a0] = OpCodeId.LDX_I.ToByte();
        mem[0xc0a1] = 0xee;

        // Act
        var outputString = OutputGen.GetLastInstructionDisassembly(cpu, mem);

        // Assert
        Assert.Equal("c0a0  a2 ee     LDX #$EE   ", outputString);
    }

    [Theory]
    [InlineData(AddrMode.Accumulator,   new byte[]{},           "A")]
    [InlineData(AddrMode.I,             new byte[]{0xee},       "#$EE")]
    [InlineData(AddrMode.ZP,            new byte[]{0x01},       "$01")]
    [InlineData(AddrMode.ZP_X,          new byte[]{0x02},       "$02,X")]
    [InlineData(AddrMode.ZP_Y,          new byte[]{0x03},       "$03,Y")]
    [InlineData(AddrMode.Relative,      new byte[]{0x00},       "*+0")]
    [InlineData(AddrMode.Relative,      new byte[]{0x04},       "*+4")]
    [InlineData(AddrMode.Relative,      new byte[]{0x80},       "*-128")]
    [InlineData(AddrMode.ABS,           new byte[]{0x10,0xc0},  "$C010")]
    [InlineData(AddrMode.ABS_X,         new byte[]{0xf0,0x80},  "$80F0,X")]
    [InlineData(AddrMode.ABS_Y,         new byte[]{0x42,0x21},  "$2142,Y")]
    [InlineData(AddrMode.Indirect,      new byte[]{0x37,0x13},  "($1337)")]
    [InlineData(AddrMode.IX_IND,        new byte[]{0x42},       "($42,X)")]
    [InlineData(AddrMode.IND_IX,        new byte[]{0x21},       "($21),Y")]
    public void OutputGen_Returns_Correctly_Formatted_Operand_String_For_AddrMode(AddrMode addrMode, byte[] operand, string expectedOutput)
    {
        // Act
        var outputString = OutputGen.BuildOperandString(addrMode, operand);

        // Assert
        Assert.Equal(expectedOutput, outputString);
    }

    [Fact]
    public void OutputGen_Returns_Correctly_Formatted_Processor_State()
    {
        // Arrange
        var cpu = new CPU();
        cpu.PC = 0x1000;
        cpu.A  = 0x00;
        cpu.X  = 0x00;
        cpu.Y  = 0x00;
        cpu.ProcessorStatus.Value = 0x00;
        cpu.SP = 0xff;

        // Act
        var outputString = OutputGen.GetProcessorState(cpu, true);

        // Assert
        Assert.Equal("A=00 X=00 Y=00 PS=[--------] SP=FF PC=1000 CY=0", outputString);
    }
    [Fact]
    public void OutputGen_Returns_Correctly_Formatted_Processor_State_When_All_Status_Flags_Are_Set        ()
    {
        // Arrange
        var cpu = new CPU();
        cpu.PC = 0x2000;
        cpu.A  = 0x01;
        cpu.X  = 0xff;
        cpu.Y  = 0x7f;
        cpu.ProcessorStatus.Value = 0xff;
        cpu.SP = 0x80;

        // Act
        var outputString = OutputGen.GetProcessorState(cpu, false);

        // Assert
        Assert.Equal("A=01 X=FF Y=7F PS=[NVUBDIZC] SP=80 PC=2000", outputString);
    }           


}
