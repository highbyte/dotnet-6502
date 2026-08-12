using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests.Instructions;

public class JMP_test
{
    [Fact]
    public void JMP_ABS_Takes_3_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.JMP_ABS,
            ExpectedCycles = 3,
        };
        test.Execute_And_Verify(AddrMode.ABS);
    }

    [Fact]
    public void JMP_Can_Jump_To_Another_Address()
    {
        // Arrange
        ushort startPos = 0x0020;
        CPU cpu = new();
        cpu.PC = startPos;
        var cpuCopy  = cpu.Clone();

        byte expectedAValue=0x42;
        ushort newPos = 0x0500;

        // Code at start address
        var mem = new Memory();
        mem.WriteByte(ref startPos, OpCodeId.JMP_ABS);
        mem.WriteWord(ref startPos, newPos);

        // Code at jmp address
        mem.WriteByte(ref newPos, OpCodeId.LDA_I);
        mem.WriteByte(ref newPos, expectedAValue);

        // Act
        cpu.Execute(mem, LegacyExecEvaluator.InstructionCountExecEvaluator(2));

        // Assert
        Assert.Equal(expectedAValue, cpu.A);
        Assert.Equal(newPos, cpu.PC);
        Assert.Equal(cpuCopy.SP, cpu.SP);
    }


    [Fact]
    public void JMP_IND_Takes_5_Cycles()
    {
        var test = new TestSpec()
        {
            OpCode         = OpCodeId.JMP_IND,
            ExpectedCycles = 5,
        };
        test.Execute_And_Verify(AddrMode.Indirect);
    }

    /// <summary>
    /// JMP is the only 6502 instruction to support indirection. 
    /// The instruction contains a 16 bit address which identifies the location of the least significant byte of another 
    /// 16 bit memory address which is the real target of the instruction.
    /// </summary>
    [Fact]
    public void JMP_IND_Can_Jump_To_Another_Address_Indirect()
    {
        // Arrange
        ushort startPos = 0x0020;
        CPU cpu = new();
        cpu.PC = startPos;
        var cpuCopy = cpu.Clone();

        ushort newPos = 0x0500;
        ushort indirectAddress = 0x0400;
        byte expectedAValue=0x42;

        // Prepare the indirect address with and address to the final jump location
        var mem = new Memory();
        mem.WriteWord(indirectAddress, newPos);

        // Code at start address
        mem.WriteByte(ref startPos, OpCodeId.JMP_IND);
        mem.WriteWord(ref startPos, indirectAddress);

        // Code at final jmp address
        mem.WriteByte(ref newPos, OpCodeId.LDA_I);
        mem.WriteByte(ref newPos, expectedAValue);

        // Act
        cpu.Execute(mem, LegacyExecEvaluator.InstructionCountExecEvaluator(2));

        // Assert
        Assert.Equal(expectedAValue, cpu.A);
        Assert.Equal(newPos, cpu.PC);
        Assert.Equal(cpuCopy.SP, cpu.SP);
    }

    /// <summary>
    /// Characterization test documenting a KNOWN NMOS DEVIATION.
    /// Real NMOS 6502 hardware has the "indirect JMP page-wrap bug": for JMP ($30FF) the
    /// high byte of the target is read from $3000 (the pointer wraps within its page),
    /// not from $3100. This emulator currently reads linearly ($3100), i.e. CMOS/65C02-style
    /// behavior. The NMOS fix is planned as an explicit behavioral change in the CPU model
    /// architecture feature (design log: cpu-models-65c02, M1 step 3) -- when that lands,
    /// this test's expectation flips to the NMOS result ($5634 below).
    /// </summary>
    [Fact]
    public void JMP_IND_With_Pointer_At_Page_End_Currently_Reads_Linearly_Known_NMOS_Deviation()
    {
        // Arrange
        ushort startPos = 0x0020;
        CPU cpu = new();
        cpu.PC = startPos;

        var mem = new Memory();
        // Indirect pointer at $30FF. The low byte of the target is at $30FF; where the
        // high byte is read from is the model-dependent part.
        mem[0x30FF] = 0x34; // target low byte
        mem[0x3100] = 0x12; // linear read location (current behavior)     -> target $1234
        mem[0x3000] = 0x56; // page-wrapped read location (real NMOS)      -> target $5634

        mem.WriteByte(ref startPos, OpCodeId.JMP_IND);
        mem.WriteWord(ref startPos, 0x30FF);

        // Act
        cpu.Execute(mem, LegacyExecEvaluator.InstructionCountExecEvaluator(1));

        // Assert: current CMOS-style (linear) result. Real NMOS would land at $5634.
        Assert.Equal((ushort)0x1234, cpu.PC);
    }
}
