namespace Highbyte.DotNet6502;

/// <summary>
/// Non-instruction behavior differences between CPU models, consumed per EVENT
/// (interrupt/BRK/reset entry), never on the per-instruction hot path.
/// </summary>
/// <param name="ClearsDecimalOnInterrupt">
/// 65C02: true — the Decimal flag is cleared on IRQ/NMI/BRK entry (after the status byte
/// is pushed) and on Reset. NMOS 6502: false — D is left as-is.
/// </param>
/// <param name="AllBytesDefined">
/// 65C02: true — all 256 opcode bytes are defined (undefined bytes are specific-length,
/// specific-cycle NOPs), so the unknown-opcode path is unreachable and the dispatch table
/// must have 256 populated entries. NMOS: false — undefined bytes depend on the
/// compatibility profile.
/// </param>
internal readonly record struct CpuModelTraits(
    bool ClearsDecimalOnInterrupt,
    bool AllBytesDefined);
