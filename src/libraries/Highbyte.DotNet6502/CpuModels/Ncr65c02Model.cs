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

        // The "(zp)" addressing mode: the eight ALU/load/store instructions gained a
        // zero-page-indirect form. Semantics are the shared instruction objects',
        // composed for the new mode; only the addressing is new. 2 bytes, 5 cycles.
        AddZpIndirectForm(table, instructionList, newCode: 0x12, existingCode: (byte)OpCodeId.ORA_I);
        AddZpIndirectForm(table, instructionList, newCode: 0x32, existingCode: (byte)OpCodeId.AND_I);
        AddZpIndirectForm(table, instructionList, newCode: 0x52, existingCode: (byte)OpCodeId.EOR_I);
        AddZpIndirectForm(table, instructionList, newCode: 0x72, existingCode: (byte)OpCodeId.ADC_I);
        AddZpIndirectForm(table, instructionList, newCode: 0x92, existingCode: (byte)OpCodeId.STA_ZP);
        AddZpIndirectForm(table, instructionList, newCode: 0xB2, existingCode: (byte)OpCodeId.LDA_I);
        AddZpIndirectForm(table, instructionList, newCode: 0xD2, existingCode: (byte)OpCodeId.CMP_I);
        AddZpIndirectForm(table, instructionList, newCode: 0xF2, existingCode: (byte)OpCodeId.SBC_I);

        // New 65C02 instructions, bound as static handlers.
        Add(table, 0x04, "TSB", AddrMode.ZP, size: 2, cycles: 5, CmosHandlers.Tsb_Zp);
        Add(table, 0x0C, "TSB", AddrMode.ABS, size: 3, cycles: 6, CmosHandlers.Tsb_Abs);
        Add(table, 0x14, "TRB", AddrMode.ZP, size: 2, cycles: 5, CmosHandlers.Trb_Zp);
        Add(table, 0x1C, "TRB", AddrMode.ABS, size: 3, cycles: 6, CmosHandlers.Trb_Abs);
        Add(table, 0x1A, "INC", AddrMode.Accumulator, size: 1, cycles: 2, CmosHandlers.Inc_Accumulator);
        Add(table, 0x3A, "DEC", AddrMode.Accumulator, size: 1, cycles: 2, CmosHandlers.Dec_Accumulator);
        Add(table, 0x34, "BIT", AddrMode.ZP_X, size: 2, cycles: 4, CmosHandlers.Bit_ZpX);
        Add(table, 0x3C, "BIT", AddrMode.ABS_X, size: 3, cycles: 4, CmosHandlers.Bit_AbsX);
        Add(table, 0x89, "BIT", AddrMode.I, size: 2, cycles: 2, CmosHandlers.Bit_Immediate);
        Add(table, 0x5A, "PHY", AddrMode.Implied, size: 1, cycles: 3, CmosHandlers.Phy);
        Add(table, 0x7A, "PLY", AddrMode.Implied, size: 1, cycles: 4, CmosHandlers.Ply);
        Add(table, 0xDA, "PHX", AddrMode.Implied, size: 1, cycles: 3, CmosHandlers.Phx);
        Add(table, 0xFA, "PLX", AddrMode.Implied, size: 1, cycles: 4, CmosHandlers.Plx);
        Add(table, 0x64, "STZ", AddrMode.ZP, size: 2, cycles: 3, CmosHandlers.Stz_Zp);
        Add(table, 0x74, "STZ", AddrMode.ZP_X, size: 2, cycles: 4, CmosHandlers.Stz_ZpX);
        Add(table, 0x9C, "STZ", AddrMode.ABS, size: 3, cycles: 4, CmosHandlers.Stz_Abs);     // SHY abs,X on NMOS
        Add(table, 0x9E, "STZ", AddrMode.ABS_X, size: 3, cycles: 5, CmosHandlers.Stz_AbsX);  // SHX abs,Y on NMOS
        Add(table, 0x7C, "JMP", AddrMode.ABS_IX_IND, size: 3, cycles: 6, CmosHandlers.Jmp_AbsIndexedIndirect);
        Add(table, 0x80, "BRA", AddrMode.Relative, size: 2, cycles: 3, CmosHandlers.Bra);

        // Every remaining byte is a defined NOP with a specific size and cycle count
        // (base/NCR part: the Rockwell/WDC extension bytes are NOPs too).
        FillDefinedNops(table);

        return table;
    }

    /// <summary>
    /// Adds a "(zp)" form of an existing instruction: the instruction object (looked up
    /// via one of its NMOS opcode bytes) is composed for the ZP_IND addressing mode.
    /// </summary>
    private static void AddZpIndirectForm(OpCodeDescriptor?[] table, InstructionList instructionList, byte newCode, byte existingCode)
    {
        var instruction = instructionList.GetInstruction(instructionList.GetOpCode(existingCode));
        // Metadata carrier for the composed handler. Note OpCode.Code is a byte-valued
        // NMOS-named enum; only the raw byte and addressing mode matter here.
        var opCode = new OpCode
        {
            Code = (OpCodeId)newCode,
            AddressingMode = AddrMode.ZP_IND,
            Size = 2,
            MinimumCycles = 5,
        };
        table[newCode] = new OpCodeDescriptor
        {
            Code = newCode,
            Mnemonic = instruction.Name,
            Addressing = AddrMode.ZP_IND,
            Size = 2,
            BaseCycles = 5,
            Documented = true,
            Execute = OpCodeDescriptorTableBuilder.ComposeExecuteHandler(opCode, instruction),
        };
    }

    private static void Add(OpCodeDescriptor?[] table, byte code, string mnemonic, AddrMode addressing, byte size, byte cycles, ExecuteHandler handler)
    {
        table[code] = new OpCodeDescriptor
        {
            Code = code,
            Mnemonic = mnemonic,
            Addressing = addressing,
            Size = size,
            BaseCycles = cycles,
            Documented = true,
            Execute = handler,
        };
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
