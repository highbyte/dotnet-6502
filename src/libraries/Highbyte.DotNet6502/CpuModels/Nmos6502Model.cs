namespace Highbyte.DotNet6502;

/// <summary>
/// The NMOS 6502 model — the behavior this emulator has always had. All four
/// compatibility profiles are supported (they control which undocumented NMOS
/// opcodes are exposed).
/// </summary>
internal static class Nmos6502Model
{
    public static readonly CpuModelDefinition Definition = new()
    {
        ModelId = CpuModelIds.Nmos6502,
        DisplayName = "NMOS 6502",
        SupportedProfiles = new[]
        {
            CpuCompatibilityProfile.OfficialOnly,
            CpuCompatibilityProfile.StableUnofficial,
            CpuCompatibilityProfile.ExperimentalUnofficial,
            CpuCompatibilityProfile.FullUnofficial,
        },
        CreateInstructionList = InstructionList.GetAllInstructions,
        Traits = new CpuModelTraits(
            ClearsDecimalOnInterrupt: false,
            AllBytesDefined: false),
    };
}
