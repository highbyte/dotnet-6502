namespace Highbyte.DotNet6502;

/// <summary>
/// The NMOS 6502 model — the behavior this emulator has always had. All four
/// compatibility profiles are supported (they control which undocumented NMOS
/// opcodes are exposed).
/// </summary>
internal static class Nmos6502Model
{
    // Single source of truth for the model's traits: referenced both by the definition
    // and by the descriptor-table build below, so the two can never disagree.
    private static readonly CpuModelTraits s_traits = new(
        ClearsDecimalOnInterrupt: false,
        AllBytesDefined: false,
        PerformsIndexedDummyReads: true);

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
        Traits = s_traits,
        CreateDescriptors = static instructionList =>
        {
            var table = OpCodeDescriptorTableBuilder.Build(
                instructionList,
                handlerOverrides: new Dictionary<byte, ExecuteHandler>
                {
                    // NMOS indirect-JMP page-wrap bug (JMP ($xxFF) reads the high byte from $xx00).
                    [(byte)OpCodeId.JMP_IND] = NmosHandlers.Jmp_Indirect,
                },
                indexedDummyReads: s_traits.PerformsIndexedDummyReads);
            // Handler migration (transitional): migrated instruction groups are re-bound
            // as core-based handlers composed for this model's dummy-read policy.
            MigratedInstructionBindings.Apply(table, s_traits.PerformsIndexedDummyReads);
            return table;
        },
    };
}
