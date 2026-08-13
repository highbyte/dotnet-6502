using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502;

/// <summary>
/// Bespoke execute handlers for instructions whose NMOS 6502 behavior doesn't fit the
/// generic core composition. Static methods — no captured state, no per-call
/// allocation, AOT-safe. Bound into the NMOS descriptor table at build time.
/// </summary>
internal static class NmosHandlers
{
    /// <summary>
    /// $6C JMP (addr) with the NMOS page-wrap bug: when the pointer sits at $xxFF, the
    /// high byte of the target is read from $xx00 (the pointer wraps within its own
    /// page) instead of the start of the next page. CMOS parts read linearly.
    /// </summary>
    public static ulong Jmp_Indirect(CPU cpu, Memory mem)
    {
        ushort pointer = cpu.FetchOperandWord(mem);
        byte targetLowByte = cpu.FetchByte(mem, pointer);
        byte targetHighByte = cpu.FetchByte(mem, (ushort)((pointer & 0xFF00) | ((pointer + 1) & 0x00FF)));
        cpu.PC = ByteHelpers.ToLittleEndianWord(targetLowByte, targetHighByte);
        return 5;
    }

    /// <summary>
    /// The JAM/KIL bytes: an NMOS decode dead-end that freezes the CPU until reset.
    /// 2 cycles accounted (the freeze itself is modelled by the halt state).
    /// </summary>
    public static ulong Jam(CPU cpu, Memory mem)
    {
        cpu.Halt();
        return 2;
    }
}
