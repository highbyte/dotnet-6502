namespace Highbyte.DotNet6502;

/// <summary>
/// Stable CPU model identifiers. These are serialized into snapshots and configuration,
/// so an id must never silently change meaning once released.
/// Further ids (mos6510, ncr65c02) are added together with their model implementations.
/// </summary>
public static class CpuModelIds
{
    public const string Nmos6502 = "nmos6502";
}
