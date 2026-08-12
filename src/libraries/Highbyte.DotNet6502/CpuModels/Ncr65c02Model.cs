namespace Highbyte.DotNet6502;

/// <summary>
/// The original/NCR-style base 65C02 — the CPU of the enhanced Apple IIe. Relative to
/// the NMOS 6502: 27 additional opcode bytes (ten new instructions), fixed JMP (addr)
/// pointer read, valid decimal flags, Decimal cleared on interrupt/reset, and EVERY
/// byte defined — bytes without an instruction are NOPs with specific sizes/cycles.
/// No Rockwell (RMB/SMB/BBR/BBS) or WDC (WAI/STP) extensions; those bytes are NOPs.
///
/// Built up in stages (design log: cpu-models-65c02 M1.4): this stage carries the shared
/// official instruction set, the CMOS JMP (addr), and the defined-NOP map. The new
/// 65C02 instructions, CMOS decimal arithmetic, and shift/rotate abs,X cycle changes
/// follow in the next stages before the model is wired into any system.
/// </summary>
internal static class Ncr65c02Model
{
    public static readonly CpuModelDefinition Definition = new()
    {
        ModelId = CpuModelIds.Ncr65c02,
        DisplayName = "NCR 65C02",
        // Compatibility profiles control undocumented-NMOS-opcode exposure; on a 65C02
        // every byte is defined, so exactly one profile is meaningful.
        SupportedProfiles = new[] { CpuCompatibilityProfile.OfficialOnly },
        // Public façade view: the officially documented instruction set shared with the
        // NMOS 6502. Per-model tooling metadata comes from the descriptor table.
        CreateInstructionList = InstructionList.GetAllInstructions,
        Traits = new CpuModelTraits(
            ClearsDecimalOnInterrupt: true,
            AllBytesDefined: true),
        CreateDescriptors = BuildDescriptors,
    };

    private static OpCodeDescriptor?[] BuildDescriptors(InstructionList instructionList)
    {
        // Start from the generic composition of the shared official instructions
        // (semantics identical on the 65C02 for these), then apply the CMOS delta.
        var table = OpCodeDescriptorTableBuilder.Build(instructionList);

        // JMP (addr): pointer read is linear (NMOS wrap bug fixed), 6 cycles (was 5).
        table[(byte)OpCodeId.JMP_IND] = new OpCodeDescriptor
        {
            Code = (byte)OpCodeId.JMP_IND,
            Mnemonic = "JMP",
            Addressing = AddrMode.Indirect,
            Size = 3,
            BaseCycles = 6,
            Documented = true,
            Execute = CmosHandlers.Jmp_Indirect,
        };

        // Every remaining byte is a defined NOP with a specific size and cycle count
        // (base/NCR part: the Rockwell/WDC extension bytes are NOPs too).
        FillDefinedNops(table);

        return table;
    }

    private static void FillDefinedNops(OpCodeDescriptor?[] table)
    {
        for (var code = 0; code <= 0xff; code++)
        {
            var b = (byte)code;
            if (table[b] is not null)
                continue;

            var (size, cycles, handler) = GetDefinedNopShape(b);
            table[b] = new OpCodeDescriptor
            {
                Code = b,
                Mnemonic = "NOP",
                Addressing = size == 1 ? AddrMode.Implied : AddrMode.I,
                Size = size,
                BaseCycles = cycles,
                Documented = false, // defined behavior, but not a documented instruction
                Execute = handler,
            };
        }
    }

    /// <summary>
    /// Size/cycles for the 65C02's defined NOPs. Sources differ on some of these; the
    /// Klaus 65C02 extended-opcodes test (compiled for the base part) is the arbiter.
    /// </summary>
    private static (byte Size, byte Cycles, ExecuteHandler Handler) GetDefinedNopShape(byte code)
        => code switch
        {
            // $x2 column leftovers: 2-byte, 2-cycle.
            0x02 or 0x22 or 0x42 or 0x62 or 0x82 or 0xC2 or 0xE2 => (2, 2, CmosHandlers.Nop_2Byte_2Cycle),
            // $44: 2-byte, 3-cycle.
            0x44 => (2, 3, CmosHandlers.Nop_2Byte_3Cycle),
            // $54, $D4, $F4: 2-byte, 4-cycle.
            0x54 or 0xD4 or 0xF4 => (2, 4, CmosHandlers.Nop_2Byte_4Cycle),
            // $5C: 3-byte, 8-cycle.
            0x5C => (3, 8, CmosHandlers.Nop_3Byte_8Cycle),
            // $DC, $FC: 3-byte, 4-cycle.
            0xDC or 0xFC => (3, 4, CmosHandlers.Nop_3Byte_4Cycle),
            // Columns $x3/$x7/$xB/$xF (Rockwell/WDC territory on other parts): 1-byte, 1-cycle.
            _ => (1, 1, CmosHandlers.Nop_1Byte_1Cycle),
        };
}
