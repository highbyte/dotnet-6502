namespace Highbyte.DotNet6502;

/// <summary>
/// Bespoke execute handlers specific to the 65C02: its JMP indirect variants, the
/// index-register stack operations, and the defined-NOP family for otherwise-undefined
/// bytes. Static methods — no captured state, no per-call allocation, AOT-safe.
/// Bound into the ncr65c02 descriptor table at build time.
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
