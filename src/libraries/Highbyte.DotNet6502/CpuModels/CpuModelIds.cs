namespace Highbyte.DotNet6502;

/// <summary>
/// Stable CPU model identifiers. These are serialized into snapshots and configuration,
/// so an id must never silently change meaning once released.
/// </summary>
public static class CpuModelIds
{
    public const string Nmos6502 = "nmos6502";
    public const string Mos6510 = "mos6510";
    public const string Ncr65c02 = "ncr65c02";
}
