using static Highbyte.DotNet6502.OpCodeDescriptorTableBuilder;

namespace Highbyte.DotNet6502;

/// <summary>
/// TRANSITIONAL (handler migration): the officially documented instruction bytes whose
/// implementations have moved from Instruction classes to operation cores
/// (<see cref="InstructionCores"/>). Applied by BOTH model table builders on top of the
/// legacy instruction-object composition, replacing those bytes with core-bound handlers
/// composed for the model's dummy-read policy. When every group has migrated, this table
/// becomes the models' primary table and the legacy composition path is deleted.
/// </summary>
internal static class MigratedInstructionBindings
{
    /// <param name="table">Model descriptor table to overwrite migrated bytes in.</param>
    /// <param name="indexedDummyReads">The model's NMOS indexed dummy-read policy.</param>
    public static void Apply(OpCodeDescriptor?[] table, bool indexedDummyReads)
    {
        // --- LDA ---
        Read(table, 0xA9, "LDA", AddrMode.I, 2, 2, InstructionCores.Lda, false, indexedDummyReads);
        Read(table, 0xA5, "LDA", AddrMode.ZP, 2, 3, InstructionCores.Lda, false, indexedDummyReads);
        Read(table, 0xB5, "LDA", AddrMode.ZP_X, 2, 4, InstructionCores.Lda, false, indexedDummyReads);
        Read(table, 0xAD, "LDA", AddrMode.ABS, 3, 4, InstructionCores.Lda, false, indexedDummyReads);
        Read(table, 0xBD, "LDA", AddrMode.ABS_X, 3, 4, InstructionCores.Lda, true, indexedDummyReads);
        Read(table, 0xB9, "LDA", AddrMode.ABS_Y, 3, 4, InstructionCores.Lda, true, indexedDummyReads);
        Read(table, 0xA1, "LDA", AddrMode.IX_IND, 2, 6, InstructionCores.Lda, false, indexedDummyReads);
        Read(table, 0xB1, "LDA", AddrMode.IND_IX, 2, 5, InstructionCores.Lda, true, indexedDummyReads);

        // --- LDX ---
        Read(table, 0xA2, "LDX", AddrMode.I, 2, 2, InstructionCores.Ldx, false, indexedDummyReads);
        Read(table, 0xA6, "LDX", AddrMode.ZP, 2, 3, InstructionCores.Ldx, false, indexedDummyReads);
        Read(table, 0xB6, "LDX", AddrMode.ZP_Y, 2, 4, InstructionCores.Ldx, false, indexedDummyReads);
        Read(table, 0xAE, "LDX", AddrMode.ABS, 3, 4, InstructionCores.Ldx, false, indexedDummyReads);
        Read(table, 0xBE, "LDX", AddrMode.ABS_Y, 3, 4, InstructionCores.Ldx, true, indexedDummyReads);

        // --- LDY ---
        Read(table, 0xA0, "LDY", AddrMode.I, 2, 2, InstructionCores.Ldy, false, indexedDummyReads);
        Read(table, 0xA4, "LDY", AddrMode.ZP, 2, 3, InstructionCores.Ldy, false, indexedDummyReads);
        Read(table, 0xB4, "LDY", AddrMode.ZP_X, 2, 4, InstructionCores.Ldy, false, indexedDummyReads);
        Read(table, 0xAC, "LDY", AddrMode.ABS, 3, 4, InstructionCores.Ldy, false, indexedDummyReads);
        Read(table, 0xBC, "LDY", AddrMode.ABS_X, 3, 4, InstructionCores.Ldy, true, indexedDummyReads);

        // --- STA ---
        Store(table, 0x85, "STA", AddrMode.ZP, 2, 3, InstructionCores.Sta, indexedDummyReads);
        Store(table, 0x95, "STA", AddrMode.ZP_X, 2, 4, InstructionCores.Sta, indexedDummyReads);
        Store(table, 0x8D, "STA", AddrMode.ABS, 3, 4, InstructionCores.Sta, indexedDummyReads);
        Store(table, 0x9D, "STA", AddrMode.ABS_X, 3, 5, InstructionCores.Sta, indexedDummyReads);
        Store(table, 0x99, "STA", AddrMode.ABS_Y, 3, 5, InstructionCores.Sta, indexedDummyReads);
        Store(table, 0x81, "STA", AddrMode.IX_IND, 2, 6, InstructionCores.Sta, indexedDummyReads);
        Store(table, 0x91, "STA", AddrMode.IND_IX, 2, 6, InstructionCores.Sta, indexedDummyReads);

        // --- STX / STY ---
        Store(table, 0x86, "STX", AddrMode.ZP, 2, 3, InstructionCores.Stx, indexedDummyReads);
        Store(table, 0x96, "STX", AddrMode.ZP_Y, 2, 4, InstructionCores.Stx, indexedDummyReads);
        Store(table, 0x8E, "STX", AddrMode.ABS, 3, 4, InstructionCores.Stx, indexedDummyReads);
        Store(table, 0x84, "STY", AddrMode.ZP, 2, 3, InstructionCores.Sty, indexedDummyReads);
        Store(table, 0x94, "STY", AddrMode.ZP_X, 2, 4, InstructionCores.Sty, indexedDummyReads);
        Store(table, 0x8C, "STY", AddrMode.ABS, 3, 4, InstructionCores.Sty, indexedDummyReads);

        // --- Register transfers ---
        Implied(table, 0xAA, "TAX", 2, InstructionCores.Tax);
        Implied(table, 0xA8, "TAY", 2, InstructionCores.Tay);
        Implied(table, 0xBA, "TSX", 2, InstructionCores.Tsx);
        Implied(table, 0x8A, "TXA", 2, InstructionCores.Txa);
        Implied(table, 0x9A, "TXS", 2, InstructionCores.Txs);
        Implied(table, 0x98, "TYA", 2, InstructionCores.Tya);
    }

    private static void Read(OpCodeDescriptor?[] table, byte code, string mnemonic, AddrMode addressing,
        byte size, byte baseCycles, ReadOperation core, bool addPageCrossCycle, bool indexedDummyReads)
        => table[code] = new OpCodeDescriptor
        {
            Code = code,
            Mnemonic = mnemonic,
            Addressing = addressing,
            Size = size,
            BaseCycles = baseCycles,
            Documented = true,
            Execute = ComposeRead(addressing, baseCycles, core, addPageCrossCycle, indexedDummyReads),
        };

    private static void Store(OpCodeDescriptor?[] table, byte code, string mnemonic, AddrMode addressing,
        byte size, byte baseCycles, StoreOperation core, bool indexedDummyReads)
        => table[code] = new OpCodeDescriptor
        {
            Code = code,
            Mnemonic = mnemonic,
            Addressing = addressing,
            Size = size,
            BaseCycles = baseCycles,
            Documented = true,
            Execute = ComposeStore(addressing, baseCycles, core, indexedDummyReads),
        };

    private static void Implied(OpCodeDescriptor?[] table, byte code, string mnemonic,
        byte baseCycles, ImpliedOperation core)
        => table[code] = new OpCodeDescriptor
        {
            Code = code,
            Mnemonic = mnemonic,
            Addressing = AddrMode.Implied,
            Size = 1,
            BaseCycles = baseCycles,
            Documented = true,
            Execute = ComposeImplied(baseCycles, core),
        };
}
