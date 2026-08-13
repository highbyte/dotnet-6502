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
        CreateDescriptors = static profile =>
        {
            // The table is composed entirely from core/handler bindings: the shared
            // official set, NMOS ADC/SBC and RMW behavior, the NMOS JMP (addr) quirk,
            // and the profile-gated undocumented opcodes.
            var table = new OpCodeDescriptor?[256];
            InstructionBindings.Apply(table, s_traits.PerformsIndexedDummyReads);
            InstructionBindings.ApplyAdcSbc(table,
                InstructionCores.AdcNmos, InstructionCores.SbcNmos,
                indexedDummyReads: s_traits.PerformsIndexedDummyReads);
            InstructionBindings.ApplyRmw(table, cmosSequence: false,
                indexedDummyReads: s_traits.PerformsIndexedDummyReads);

            // JMP (addr) with the NMOS page-wrap bug: when the pointer sits at $xxFF,
            // the high byte is read from $xx00. 5 cycles (the 65C02 takes 6).
            table[(byte)OpCodeId.JMP_IND] = new OpCodeDescriptor
            {
                Code = (byte)OpCodeId.JMP_IND,
                Mnemonic = "JMP",
                Addressing = AddrMode.Indirect,
                Size = 3,
                BaseCycles = 5,
                Documented = true,
                Execute = NmosHandlers.Jmp_Indirect,
            };

            InstructionBindings.ApplyNmosUndocumented(table, profile,
                indexedDummyReads: s_traits.PerformsIndexedDummyReads);
            return table;
        },
    };
}
