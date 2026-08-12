using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Tests for the 27 opcode bytes the NCR 65C02 adds over the NMOS 6502
/// during the CPU model architecture work.
/// </summary>
public class Ncr65c02NewInstructionsTests
{
    private const ushort StartPc = 0x1000;

    private static (CPU cpu, Memory mem) New65c02CpuAt(ushort pc = StartPc)
    {
        var cpu = new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly);
        var mem = new Memory();
        cpu.PC = pc;
        cpu.SP = 0xFF;
        return (cpu, mem);
    }

    // ---- "(zp)" zero-page-indirect forms of existing instructions ----

    [Fact]
    public void LDA_ZpIndirect_Loads_Via_ZeroPage_Pointer()
    {
        var (cpu, mem) = New65c02CpuAt();
        mem[StartPc] = 0xB2;      // LDA (zp)
        mem[StartPc + 1] = 0x40;  // zp pointer location
        mem.WriteWord(0x0040, 0x1234);
        mem[0x1234] = 0x99;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x99, cpu.A);
        Assert.True(cpu.ProcessorStatus.Negative);
        Assert.False(cpu.ProcessorStatus.Zero);
        Assert.Equal(5ul, result.CyclesConsumed);
        Assert.Equal((ushort)(StartPc + 2), cpu.PC);
    }

    [Fact]
    public void STA_ZpIndirect_Stores_Via_ZeroPage_Pointer()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0x77;
        mem[StartPc] = 0x92;      // STA (zp)
        mem[StartPc + 1] = 0x40;
        mem.WriteWord(0x0040, 0x1234);

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x77, mem[0x1234]);
        Assert.Equal(5ul, result.CyclesConsumed);
    }

    [Theory]
    [InlineData(0x12, 0b0101_0000, 0b0000_1111, 0b0101_1111)] // ORA (zp)
    [InlineData(0x32, 0b0101_0000, 0b0111_0000, 0b0101_0000)] // AND (zp)
    [InlineData(0x52, 0b0101_0000, 0b0111_0000, 0b0010_0000)] // EOR (zp)
    public void Logical_ZpIndirect_Forms_Operate_On_A(byte opCode, byte a, byte operand, byte expectedA)
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = a;
        mem[StartPc] = opCode;
        mem[StartPc + 1] = 0x40;
        mem.WriteWord(0x0040, 0x1234);
        mem[0x1234] = operand;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(expectedA, cpu.A);
        Assert.Equal(5ul, result.CyclesConsumed);
    }

    [Fact]
    public void ADC_ZpIndirect_Adds_With_Carry()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0x10;
        mem[StartPc] = 0x72;      // ADC (zp)
        mem[StartPc + 1] = 0x40;
        mem.WriteWord(0x0040, 0x1234);
        mem[0x1234] = 0x05;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x15, cpu.A);
        Assert.Equal(5ul, result.CyclesConsumed);
    }

    [Fact]
    public void CMP_ZpIndirect_Compares_And_Sets_Flags()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0x42;
        mem[StartPc] = 0xD2;      // CMP (zp)
        mem[StartPc + 1] = 0x40;
        mem.WriteWord(0x0040, 0x1234);
        mem[0x1234] = 0x42;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.True(cpu.ProcessorStatus.Zero);
        Assert.True(cpu.ProcessorStatus.Carry);
        Assert.Equal(5ul, result.CyclesConsumed);
    }

    [Fact]
    public void SBC_ZpIndirect_Subtracts_With_Borrow()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0x10;
        cpu.ProcessorStatus.Carry = true; // no borrow
        mem[StartPc] = 0xF2;      // SBC (zp)
        mem[StartPc + 1] = 0x40;
        mem.WriteWord(0x0040, 0x1234);
        mem[0x1234] = 0x05;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x0B, cpu.A);
        Assert.Equal(5ul, result.CyclesConsumed);
    }

    // ---- TSB / TRB ----

    [Fact]
    public void TSB_Zp_Sets_Bits_And_Z_From_A_And_M()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0b0000_1111;
        mem[StartPc] = 0x04;      // TSB zp
        mem[StartPc + 1] = 0x40;
        mem[0x0040] = 0b1111_0000;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0b1111_1111, mem[0x0040]);
        Assert.True(cpu.ProcessorStatus.Zero); // A AND original M == 0
        Assert.Equal(5ul, result.CyclesConsumed);
    }

    [Fact]
    public void TSB_Abs_Clears_Z_When_A_And_M_Overlap()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0b0011_0000;
        mem[StartPc] = 0x0C;      // TSB abs
        mem.WriteWord((ushort)(StartPc + 1), 0x1234);
        mem[0x1234] = 0b0001_0000;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0b0011_0000, mem[0x1234]);
        Assert.False(cpu.ProcessorStatus.Zero);
        Assert.Equal(6ul, result.CyclesConsumed);
    }

    [Fact]
    public void TRB_Zp_Resets_Bits_And_Sets_Z_From_A_And_M()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0b0000_1111;
        mem[StartPc] = 0x14;      // TRB zp
        mem[StartPc + 1] = 0x40;
        mem[0x0040] = 0b0101_0101;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0b0101_0000, mem[0x0040]);
        Assert.False(cpu.ProcessorStatus.Zero); // A AND original M != 0
        Assert.Equal(5ul, result.CyclesConsumed);
    }

    [Fact]
    public void TRB_Abs_Takes_6_Cycles()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0xFF;
        mem[StartPc] = 0x1C;      // TRB abs
        mem.WriteWord((ushort)(StartPc + 1), 0x1234);
        mem[0x1234] = 0xFF;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x00, mem[0x1234]);
        Assert.Equal(6ul, result.CyclesConsumed);
    }

    // ---- INC A / DEC A ----

    [Fact]
    public void INC_Accumulator_Increments_And_Sets_Flags()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0x7F;
        mem[StartPc] = 0x1A;      // INC A

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x80, cpu.A);
        Assert.True(cpu.ProcessorStatus.Negative);
        Assert.Equal(2ul, result.CyclesConsumed);
        Assert.Equal((ushort)(StartPc + 1), cpu.PC);
    }

    [Fact]
    public void DEC_Accumulator_Decrements_And_Sets_Zero()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0x01;
        mem[StartPc] = 0x3A;      // DEC A

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x00, cpu.A);
        Assert.True(cpu.ProcessorStatus.Zero);
        Assert.Equal(2ul, result.CyclesConsumed);
    }

    // ---- BIT new modes ----

    [Fact]
    public void BIT_Immediate_Affects_Only_Z()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0x0F;
        cpu.ProcessorStatus.Negative = true;  // must survive
        cpu.ProcessorStatus.Overflow = true;  // must survive
        mem[StartPc] = 0x89;      // BIT #
        mem[StartPc + 1] = 0xF0;  // A AND value == 0 -> Z set

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.True(cpu.ProcessorStatus.Zero);
        Assert.True(cpu.ProcessorStatus.Negative);  // unchanged (the $89 quirk)
        Assert.True(cpu.ProcessorStatus.Overflow);  // unchanged
        Assert.Equal(2ul, result.CyclesConsumed);
    }

    [Fact]
    public void BIT_ZpX_Uses_Normal_BIT_Semantics()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0x01;
        cpu.X = 0x05;
        mem[StartPc] = 0x34;      // BIT zp,X
        mem[StartPc + 1] = 0x40;
        mem[0x0045] = 0b1100_0000; // N and V from memory bits 7/6

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.True(cpu.ProcessorStatus.Zero);      // A AND M == 0
        Assert.True(cpu.ProcessorStatus.Negative);
        Assert.True(cpu.ProcessorStatus.Overflow);
        Assert.Equal(4ul, result.CyclesConsumed);
    }

    [Theory]
    [InlineData(0x00, 4ul)] // no page cross
    [InlineData(0xFF, 5ul)] // $12FF + X crosses page -> +1 cycle
    public void BIT_AbsX_Adds_Cycle_On_Page_Cross(byte x, ulong expectedCycles)
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0xFF;
        cpu.X = x;
        mem[StartPc] = 0x3C;      // BIT abs,X
        mem.WriteWord((ushort)(StartPc + 1), 0x12FF);
        mem[(ushort)(0x12FF + x)] = 0x01;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.False(cpu.ProcessorStatus.Zero);
        Assert.Equal(expectedCycles, result.CyclesConsumed);
    }

    // ---- PHX / PHY / PLX / PLY ----

    [Fact]
    public void PHX_And_PLX_Round_Trip_Through_The_Stack()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.X = 0x42;
        mem[StartPc] = 0xDA;      // PHX
        mem[StartPc + 1] = 0xFA;  // PLX

        var push = cpu.ExecuteOneInstructionMinimal(mem);
        cpu.X = 0x00;
        var pull = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x42, cpu.X);
        Assert.False(cpu.ProcessorStatus.Zero);
        Assert.Equal(3ul, push.CyclesConsumed);
        Assert.Equal(4ul, pull.CyclesConsumed);
        Assert.Equal(0xFF, cpu.SP); // balanced
    }

    [Fact]
    public void PHY_And_PLY_Round_Trip_Through_The_Stack()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.Y = 0x80;
        mem[StartPc] = 0x5A;      // PHY
        mem[StartPc + 1] = 0x7A;  // PLY

        var push = cpu.ExecuteOneInstructionMinimal(mem);
        cpu.Y = 0x00;
        var pull = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x80, cpu.Y);
        Assert.True(cpu.ProcessorStatus.Negative); // PLY sets N/Z
        Assert.Equal(3ul, push.CyclesConsumed);
        Assert.Equal(4ul, pull.CyclesConsumed);
    }

    // ---- STZ ----

    [Theory]
    [InlineData(0x64, 3ul)] // STZ zp
    [InlineData(0x74, 4ul)] // STZ zp,X
    public void STZ_ZeroPage_Forms_Store_Zero(byte opCode, ulong expectedCycles)
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.X = 0x02;
        mem[StartPc] = opCode;
        mem[StartPc + 1] = 0x40;
        var target = (ushort)(opCode == 0x74 ? 0x42 : 0x40);
        mem[target] = 0xFF;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x00, mem[target]);
        Assert.Equal(expectedCycles, result.CyclesConsumed);
    }

    [Theory]
    [InlineData(0x9C, 4ul)] // STZ abs ($9C = SHY abs,X on NMOS -- the byte-redefinition example)
    [InlineData(0x9E, 5ul)] // STZ abs,X
    public void STZ_Absolute_Forms_Store_Zero(byte opCode, ulong expectedCycles)
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.X = 0x02;
        mem[StartPc] = opCode;
        mem.WriteWord((ushort)(StartPc + 1), 0x1234);
        var target = (ushort)(opCode == 0x9E ? 0x1236 : 0x1234);
        mem[target] = 0xFF;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x00, mem[target]);
        Assert.Equal(expectedCycles, result.CyclesConsumed);
    }

    // ---- JMP (abs,X) ----

    [Fact]
    public void JMP_AbsIndexedIndirect_Jumps_Via_Indexed_Pointer_Table()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.X = 0x04;
        mem[StartPc] = 0x7C;      // JMP (abs,X)
        mem.WriteWord((ushort)(StartPc + 1), 0x3000);
        mem.WriteWord(0x3004, 0x5678); // pointer table entry at $3000 + X

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal((ushort)0x5678, cpu.PC);
        Assert.Equal(6ul, result.CyclesConsumed);
    }

    // ---- BRA ----

    [Fact]
    public void BRA_Branches_Always_Forward()
    {
        var (cpu, mem) = New65c02CpuAt();
        mem[StartPc] = 0x80;      // BRA +$10
        mem[StartPc + 1] = 0x10;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal((ushort)(StartPc + 2 + 0x10), cpu.PC);
        Assert.Equal(3ul, result.CyclesConsumed);
    }

    [Fact]
    public void BRA_Adds_Cycle_On_Page_Cross()
    {
        var (cpu, mem) = New65c02CpuAt(0x10F0);
        mem[0x10F0] = 0x80;       // BRA +$20 -> crosses into $11xx
        mem[0x10F1] = 0x20;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal((ushort)0x1112, cpu.PC);
        Assert.Equal(4ul, result.CyclesConsumed);
    }
}
