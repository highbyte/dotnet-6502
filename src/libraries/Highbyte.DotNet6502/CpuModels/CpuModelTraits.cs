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
/// <param name="PerformsIndexedDummyReads">
/// NMOS: true — indexed addressing performs a dummy read at the "un-carried" address
/// (high byte not yet corrected) when a read crosses a page, and ALWAYS before indexed
/// stores and indexed RMW instructions. 65C02: false — the CMOS part re-targeted those
/// dummy cycles (different addresses); modelling them is deferred until something
/// observable needs them. Consumed at table BUILD time, never per instruction.
/// </param>
internal readonly record struct CpuModelTraits(
    bool ClearsDecimalOnInterrupt,
    bool AllBytesDefined,
    bool PerformsIndexedDummyReads);
