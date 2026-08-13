namespace Highbyte.DotNet6502;

/// <summary>
/// Marker for <see cref="IInstructionUsesByte"/> instructions that are read-modify-write
/// (INC/DEC and the undocumented RMW combos): on NMOS models their indexed forms ALWAYS
/// perform the dummy read at the un-carried address — unlike plain reads, which dummy-read
/// only when the page crosses. Consulted at table BUILD time by the descriptor composer.
/// (The shift/rotate RMW instructions are <see cref="IInstructionUsesAddress"/>, which the
/// composer already treats as always-dummy-read on indexed modes; transitional until the
/// handler migration retires the instruction classes.)
/// </summary>
internal interface IReadModifyWriteInstruction
{
}
