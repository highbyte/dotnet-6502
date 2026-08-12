using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502;

/// <summary>
/// Execute handlers for instructions whose CMOS 65C02 behavior diverges from the generic
/// (instruction-object based) composition, plus the 65C02's defined-NOP family for
/// otherwise-undefined bytes. Static methods — no captured state, no per-call
/// allocation, AOT-safe. Bound into the ncr65c02 descriptor table at build time.
/// </summary>
internal static class CmosHandlers
{
    /// <summary>
    /// $6C JMP (addr) on the 65C02: the pointer is always read linearly (the NMOS
    /// page-wrap bug is fixed), at the cost of one extra cycle (6 vs 5).
    /// </summary>
    public static ulong Jmp_Indirect(CPU cpu, Memory mem)
    {
        ushort pointer = cpu.FetchOperandWord(mem);
        cpu.PC = cpu.FetchWord(mem, pointer);
        return 6;
    }

    /// <summary>
    /// $7C JMP (abs,X): the pointer is the absolute operand plus X; the target is read
    /// linearly from there. New 65C02 addressing mode, 6 cycles.
    /// </summary>
    public static ulong Jmp_AbsIndexedIndirect(CPU cpu, Memory mem)
    {
        var pointer = (ushort)(cpu.FetchOperandWord(mem) + cpu.X);
        cpu.PC = cpu.FetchWord(mem, pointer);
        return 6;
    }

    /// <summary>$80 BRA rel: branch always. 3 cycles, +1 on page cross.</summary>
    public static ulong Bra(CPU cpu, Memory mem)
    {
        var value = cpu.FetchOperand(mem);
        cpu.PC = BranchHelper.CalculateNewAbsoluteBranchAddress(cpu.PC, (sbyte)value, out _, out var crossedPageBoundary);
        return crossedPageBoundary ? 4ul : 3ul;
    }

    /// <summary>$1A INC A: increment the accumulator (no NMOS equivalent). 2 cycles.</summary>
    public static ulong Inc_Accumulator(CPU cpu, Memory mem)
    {
        cpu.A++;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
        return 2;
    }

    /// <summary>$3A DEC A: decrement the accumulator. 2 cycles.</summary>
    public static ulong Dec_Accumulator(CPU cpu, Memory mem)
    {
        cpu.A--;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.A, ref cpu.ProcessorStatus);
        return 2;
    }

    /// <summary>$DA PHX: push X. 3 cycles.</summary>
    public static ulong Phx(CPU cpu, Memory mem)
    {
        cpu.PushByteToStack(cpu.X, mem);
        return 3;
    }

    /// <summary>$5A PHY: push Y. 3 cycles.</summary>
    public static ulong Phy(CPU cpu, Memory mem)
    {
        cpu.PushByteToStack(cpu.Y, mem);
        return 3;
    }

    /// <summary>$FA PLX: pull X, setting N and Z. 4 cycles.</summary>
    public static ulong Plx(CPU cpu, Memory mem)
    {
        cpu.X = cpu.PopByteFromStack(mem);
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.X, ref cpu.ProcessorStatus);
        return 4;
    }

    /// <summary>$7A PLY: pull Y, setting N and Z. 4 cycles.</summary>
    public static ulong Ply(CPU cpu, Memory mem)
    {
        cpu.Y = cpu.PopByteFromStack(mem);
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(cpu.Y, ref cpu.ProcessorStatus);
        return 4;
    }

    /// <summary>$64 STZ zp: store zero. 3 cycles.</summary>
    public static ulong Stz_Zp(CPU cpu, Memory mem)
    {
        cpu.StoreByte(0, mem, cpu.FetchOperand(mem));
        return 3;
    }

    /// <summary>$74 STZ zp,X. 4 cycles.</summary>
    public static ulong Stz_ZpX(CPU cpu, Memory mem)
    {
        cpu.StoreByte(0, mem, cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem), wrapZeroPage: true));
        return 4;
    }

    /// <summary>$9C STZ abs (the byte that is SHY abs,X on NMOS). 4 cycles.</summary>
    public static ulong Stz_Abs(CPU cpu, Memory mem)
    {
        cpu.StoreByte(0, mem, cpu.FetchOperandWord(mem));
        return 4;
    }

    /// <summary>$9E STZ abs,X (the byte that is SHX abs,Y on NMOS). Always 5 cycles (store).</summary>
    public static ulong Stz_AbsX(CPU cpu, Memory mem)
    {
        cpu.StoreByte(0, mem, cpu.CalcFullAddressX(cpu.FetchOperandWord(mem), out _));
        return 5;
    }

    /// <summary>$04 TSB zp: Z = (A AND M) == 0, then M |= A. 5 cycles.</summary>
    public static ulong Tsb_Zp(CPU cpu, Memory mem) => TestAndSetBits(cpu, mem, cpu.FetchOperand(mem), 5);

    /// <summary>$0C TSB abs. 6 cycles.</summary>
    public static ulong Tsb_Abs(CPU cpu, Memory mem) => TestAndSetBits(cpu, mem, cpu.FetchOperandWord(mem), 6);

    /// <summary>$14 TRB zp: Z = (A AND M) == 0, then M &amp;= ~A. 5 cycles.</summary>
    public static ulong Trb_Zp(CPU cpu, Memory mem) => TestAndResetBits(cpu, mem, cpu.FetchOperand(mem), 5);

    /// <summary>$1C TRB abs. 6 cycles.</summary>
    public static ulong Trb_Abs(CPU cpu, Memory mem) => TestAndResetBits(cpu, mem, cpu.FetchOperandWord(mem), 6);

    private static ulong TestAndSetBits(CPU cpu, Memory mem, ushort address, ulong cycles)
    {
        var value = cpu.FetchByte(mem, address);
        cpu.ProcessorStatus.Zero = (cpu.A & value) == 0;
        cpu.StoreByte((byte)(value | cpu.A), mem, address);
        return cycles;
    }

    private static ulong TestAndResetBits(CPU cpu, Memory mem, ushort address, ulong cycles)
    {
        var value = cpu.FetchByte(mem, address);
        cpu.ProcessorStatus.Zero = (cpu.A & value) == 0;
        cpu.StoreByte((byte)(value & ~cpu.A), mem, address);
        return cycles;
    }

    /// <summary>
    /// $89 BIT #: unlike the other BIT modes, ONLY Z is affected (N and V are left
    /// unchanged) — the 65C02's documented quirk for the immediate form. 2 cycles.
    /// </summary>
    public static ulong Bit_Immediate(CPU cpu, Memory mem)
    {
        var value = cpu.FetchOperand(mem);
        cpu.ProcessorStatus.Zero = (cpu.A & value) == 0;
        return 2;
    }

    /// <summary>$34 BIT zp,X: normal BIT semantics (Z from A AND M; N/V from M). 4 cycles.</summary>
    public static ulong Bit_ZpX(CPU cpu, Memory mem)
    {
        var value = cpu.FetchByte(mem, cpu.CalcZeroPageAddressX(cpu.FetchOperand(mem), wrapZeroPage: true));
        BinaryArithmeticHelpers.PerformBITAndSetStatusRegisters(cpu.A, value, ref cpu.ProcessorStatus);
        return 4;
    }

    /// <summary>$3C BIT abs,X: normal BIT semantics. 4 cycles, +1 on page cross.</summary>
    public static ulong Bit_AbsX(CPU cpu, Memory mem)
    {
        var value = cpu.FetchByte(mem, cpu.CalcFullAddressX(cpu.FetchOperandWord(mem), out var crossedPageBoundary));
        BinaryArithmeticHelpers.PerformBITAndSetStatusRegisters(cpu.A, value, ref cpu.ProcessorStatus);
        return crossedPageBoundary ? 5ul : 4ul;
    }

    // The 65C02 defines every byte: bytes without an assigned instruction execute as
    // NOPs with specific sizes and cycle counts (per the base/NCR part; the Klaus 65C02
    // extended-opcodes test is the arbiter for these values). The handler consumes the
    // operand bytes by advancing PC and does nothing else.

    /// <summary>1-byte, 1-cycle NOP (columns $x3, $x7, $xB, $xF on the base 65C02).</summary>
    public static ulong Nop_1Byte_1Cycle(CPU cpu, Memory mem) => 1;

    /// <summary>2-byte, 2-cycle NOP (undefined $x2 column bytes).</summary>
    public static ulong Nop_2Byte_2Cycle(CPU cpu, Memory mem)
    {
        cpu.PC++;
        return 2;
    }

    /// <summary>2-byte, 3-cycle NOP ($44).</summary>
    public static ulong Nop_2Byte_3Cycle(CPU cpu, Memory mem)
    {
        cpu.PC++;
        return 3;
    }

    /// <summary>2-byte, 4-cycle NOP ($54, $D4, $F4).</summary>
    public static ulong Nop_2Byte_4Cycle(CPU cpu, Memory mem)
    {
        cpu.PC++;
        return 4;
    }

    /// <summary>3-byte, 4-cycle NOP ($DC, $FC).</summary>
    public static ulong Nop_3Byte_4Cycle(CPU cpu, Memory mem)
    {
        cpu.PC += 2;
        return 4;
    }

    /// <summary>3-byte, 8-cycle NOP ($5C — the 65C02's oddest defined NOP).</summary>
    public static ulong Nop_3Byte_8Cycle(CPU cpu, Memory mem)
    {
        cpu.PC += 2;
        return 8;
    }
}
