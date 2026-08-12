using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Tests for the 65C02's decimal-mode ADC/SBC (valid N/Z flags, own SBC correction
/// sequence, +1 cycle) and the shift/rotate abs,X cycle change
/// (feature cpu-models-65c02, M1 step 4). Counterparts of the NMOS decimal tests in
/// ADC_test/SBC_test where the models deliberately differ.
/// </summary>
public class Ncr65c02ArithmeticAndCycleTests
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

    // ---- ADC decimal mode ----

    [Fact]
    public void ADC_Decimal_99_Plus_01_Has_Valid_Zero_Flag_And_Takes_3_Cycles()
    {
        // 99 + 01 = 00 with carry. The NMOS Z flag would be 0 (from the binary sum $9A);
        // the 65C02 Z flag is valid: 1, from the decimal result $00. N likewise valid: 0
        // (the NMOS computes N=1 from its signed intermediate here).
        var (cpu, mem) = New65c02CpuAt();
        cpu.ProcessorStatus.Decimal = true;
        cpu.ProcessorStatus.Carry = false;
        cpu.A = 0x99;
        mem[StartPc] = (byte)OpCodeId.ADC_I;
        mem[StartPc + 1] = 0x01;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x00, cpu.A);
        Assert.True(cpu.ProcessorStatus.Carry);
        Assert.True(cpu.ProcessorStatus.Zero);      // valid on 65C02
        Assert.False(cpu.ProcessorStatus.Negative); // valid on 65C02
        Assert.False(cpu.ProcessorStatus.Overflow);
        Assert.Equal(3ul, result.CyclesConsumed);   // 2 + 1 decimal-mode cycle
    }

    [Fact]
    public void ADC_Decimal_90_Plus_90_Has_Valid_Negative_Flag()
    {
        // 90 + 90 = 80 with carry. 65C02 N comes from the final result $80: N=1.
        var (cpu, mem) = New65c02CpuAt();
        cpu.ProcessorStatus.Decimal = true;
        cpu.ProcessorStatus.Carry = false;
        cpu.A = 0x90;
        mem[StartPc] = (byte)OpCodeId.ADC_I;
        mem[StartPc + 1] = 0x90;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x80, cpu.A);
        Assert.True(cpu.ProcessorStatus.Carry);
        Assert.True(cpu.ProcessorStatus.Negative);
        Assert.False(cpu.ProcessorStatus.Zero);
        Assert.True(cpu.ProcessorStatus.Overflow); // signed intermediate out of range
        Assert.Equal(3ul, result.CyclesConsumed);
    }

    [Fact]
    public void ADC_Binary_Mode_Still_Takes_2_Cycles()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.A = 0x12;
        mem[StartPc] = (byte)OpCodeId.ADC_I;
        mem[StartPc + 1] = 0x34;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x46, cpu.A);
        Assert.Equal(2ul, result.CyclesConsumed);
    }

    [Fact]
    public void ADC_ZpIndirect_Decimal_Also_Gets_The_Extra_Cycle()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.ProcessorStatus.Decimal = true;
        cpu.A = 0x25;
        mem[StartPc] = 0x72;      // ADC (zp)
        mem[StartPc + 1] = 0x40;
        mem.WriteWord(0x0040, 0x1234);
        mem[0x1234] = 0x25;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x50, cpu.A);
        Assert.Equal(6ul, result.CyclesConsumed); // 5 + 1 decimal-mode cycle
    }

    // ---- SBC decimal mode ----

    [Fact]
    public void SBC_Decimal_00_Minus_01_Wraps_To_99_With_Valid_Negative_Flag()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.ProcessorStatus.Decimal = true;
        cpu.ProcessorStatus.Carry = true; // no borrow
        cpu.A = 0x00;
        mem[StartPc] = (byte)OpCodeId.SBC_I;
        mem[StartPc + 1] = 0x01;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x99, cpu.A);
        Assert.False(cpu.ProcessorStatus.Carry);   // borrow occurred (binary C)
        Assert.True(cpu.ProcessorStatus.Negative); // valid: from decimal result $99
        Assert.False(cpu.ProcessorStatus.Zero);
        Assert.Equal(3ul, result.CyclesConsumed);
    }

    [Fact]
    public void SBC_Decimal_Equal_Operands_Set_Valid_Zero_Flag()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.ProcessorStatus.Decimal = true;
        cpu.ProcessorStatus.Carry = true;
        cpu.A = 0x42;
        mem[StartPc] = (byte)OpCodeId.SBC_I;
        mem[StartPc + 1] = 0x42;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x00, cpu.A);
        Assert.True(cpu.ProcessorStatus.Carry);
        Assert.True(cpu.ProcessorStatus.Zero);
        Assert.False(cpu.ProcessorStatus.Negative);
        Assert.Equal(3ul, result.CyclesConsumed);
    }

    [Fact]
    public void SBC_Binary_Mode_Still_Takes_2_Cycles()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.ProcessorStatus.Carry = true;
        cpu.A = 0x40;
        mem[StartPc] = (byte)OpCodeId.SBC_I;
        mem[StartPc + 1] = 0x13;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x2D, cpu.A);
        Assert.Equal(2ul, result.CyclesConsumed);
    }

    // ---- Shift/rotate abs,X cycles ----

    [Theory]
    [InlineData((byte)OpCodeId.ASL_ABS_X)]
    [InlineData((byte)OpCodeId.ROL_ABS_X)]
    [InlineData((byte)OpCodeId.LSR_ABS_X)]
    [InlineData((byte)OpCodeId.ROR_ABS_X)]
    public void Shift_AbsX_Takes_6_Cycles_Without_Page_Cross(byte opCode)
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.X = 0x01;
        mem[StartPc] = opCode;
        mem.WriteWord((ushort)(StartPc + 1), 0x1234);
        mem[0x1235] = 0b0000_0100;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(6ul, result.CyclesConsumed); // NMOS: always 7
        Assert.NotEqual(0b0000_0100, mem[0x1235]); // shifted/rotated
    }

    [Fact]
    public void Shift_AbsX_Takes_7_Cycles_With_Page_Cross()
    {
        var (cpu, mem) = New65c02CpuAt();
        cpu.X = 0xFF;
        mem[StartPc] = (byte)OpCodeId.ASL_ABS_X;
        mem.WriteWord((ushort)(StartPc + 1), 0x12FF);
        mem[0x13FE] = 0b0000_0100;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(7ul, result.CyclesConsumed);
        Assert.Equal(0b0000_1000, mem[0x13FE]);
    }

    [Fact]
    public void INC_AbsX_Still_Takes_7_Cycles_On_The_65C02()
    {
        // The 65C02 only changed the shifts/rotates; INC/DEC abs,X remain 7 cycles.
        var (cpu, mem) = New65c02CpuAt();
        cpu.X = 0x01;
        mem[StartPc] = (byte)OpCodeId.INC_ABS_X;
        mem.WriteWord((ushort)(StartPc + 1), 0x1234);
        mem[0x1235] = 0x10;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x11, mem[0x1235]);
        Assert.Equal(7ul, result.CyclesConsumed);
    }
}
