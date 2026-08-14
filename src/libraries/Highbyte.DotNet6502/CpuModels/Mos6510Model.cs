namespace Highbyte.DotNet6502;

/// <summary>
/// The MOS 6510 — the C64's CPU: an NMOS 6502 core plus an on-chip I/O port that the
/// machine maps at $00/$01. Instruction behavior, bus sequences, traits, and
/// compatibility profiles are identical to the NMOS 6502 model (the members below are
/// shared with it directly); the difference is the per-CPU-instance port state
/// (<see cref="Cpu6510Port"/>) supplied by the state factory.
/// </summary>
internal static class Mos6510Model
{
    public static readonly CpuModelDefinition Definition = new()
    {
        ModelId = CpuModelIds.Mos6510,
        DisplayName = "MOS 6510",
        SupportedProfiles = Nmos6502Model.Definition.SupportedProfiles,
        Traits = Nmos6502Model.Definition.Traits,
        CreateDescriptors = Nmos6502Model.Definition.CreateDescriptors,
        StateFactory = static () => new Cpu6510Port(),
    };
}
