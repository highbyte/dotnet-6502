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

        // --- Logic ---
        Read(table, 0x29, "AND", AddrMode.I, 2, 2, InstructionCores.And, false, indexedDummyReads);
        Read(table, 0x25, "AND", AddrMode.ZP, 2, 3, InstructionCores.And, false, indexedDummyReads);
        Read(table, 0x35, "AND", AddrMode.ZP_X, 2, 4, InstructionCores.And, false, indexedDummyReads);
        Read(table, 0x2D, "AND", AddrMode.ABS, 3, 4, InstructionCores.And, false, indexedDummyReads);
        Read(table, 0x3D, "AND", AddrMode.ABS_X, 3, 4, InstructionCores.And, true, indexedDummyReads);
        Read(table, 0x39, "AND", AddrMode.ABS_Y, 3, 4, InstructionCores.And, true, indexedDummyReads);
        Read(table, 0x21, "AND", AddrMode.IX_IND, 2, 6, InstructionCores.And, false, indexedDummyReads);
        Read(table, 0x31, "AND", AddrMode.IND_IX, 2, 5, InstructionCores.And, true, indexedDummyReads);

        Read(table, 0x09, "ORA", AddrMode.I, 2, 2, InstructionCores.Ora, false, indexedDummyReads);
        Read(table, 0x05, "ORA", AddrMode.ZP, 2, 3, InstructionCores.Ora, false, indexedDummyReads);
        Read(table, 0x15, "ORA", AddrMode.ZP_X, 2, 4, InstructionCores.Ora, false, indexedDummyReads);
        Read(table, 0x0D, "ORA", AddrMode.ABS, 3, 4, InstructionCores.Ora, false, indexedDummyReads);
        Read(table, 0x1D, "ORA", AddrMode.ABS_X, 3, 4, InstructionCores.Ora, true, indexedDummyReads);
        Read(table, 0x19, "ORA", AddrMode.ABS_Y, 3, 4, InstructionCores.Ora, true, indexedDummyReads);
        Read(table, 0x01, "ORA", AddrMode.IX_IND, 2, 6, InstructionCores.Ora, false, indexedDummyReads);
        Read(table, 0x11, "ORA", AddrMode.IND_IX, 2, 5, InstructionCores.Ora, true, indexedDummyReads);

        Read(table, 0x49, "EOR", AddrMode.I, 2, 2, InstructionCores.Eor, false, indexedDummyReads);
        Read(table, 0x45, "EOR", AddrMode.ZP, 2, 3, InstructionCores.Eor, false, indexedDummyReads);
        Read(table, 0x55, "EOR", AddrMode.ZP_X, 2, 4, InstructionCores.Eor, false, indexedDummyReads);
        Read(table, 0x4D, "EOR", AddrMode.ABS, 3, 4, InstructionCores.Eor, false, indexedDummyReads);
        Read(table, 0x5D, "EOR", AddrMode.ABS_X, 3, 4, InstructionCores.Eor, true, indexedDummyReads);
        Read(table, 0x59, "EOR", AddrMode.ABS_Y, 3, 4, InstructionCores.Eor, true, indexedDummyReads);
        Read(table, 0x41, "EOR", AddrMode.IX_IND, 2, 6, InstructionCores.Eor, false, indexedDummyReads);
        Read(table, 0x51, "EOR", AddrMode.IND_IX, 2, 5, InstructionCores.Eor, true, indexedDummyReads);

        // --- Compares ---
        Read(table, 0xC9, "CMP", AddrMode.I, 2, 2, InstructionCores.Cmp, false, indexedDummyReads);
        Read(table, 0xC5, "CMP", AddrMode.ZP, 2, 3, InstructionCores.Cmp, false, indexedDummyReads);
        Read(table, 0xD5, "CMP", AddrMode.ZP_X, 2, 4, InstructionCores.Cmp, false, indexedDummyReads);
        Read(table, 0xCD, "CMP", AddrMode.ABS, 3, 4, InstructionCores.Cmp, false, indexedDummyReads);
        Read(table, 0xDD, "CMP", AddrMode.ABS_X, 3, 4, InstructionCores.Cmp, true, indexedDummyReads);
        Read(table, 0xD9, "CMP", AddrMode.ABS_Y, 3, 4, InstructionCores.Cmp, true, indexedDummyReads);
        Read(table, 0xC1, "CMP", AddrMode.IX_IND, 2, 6, InstructionCores.Cmp, false, indexedDummyReads);
        Read(table, 0xD1, "CMP", AddrMode.IND_IX, 2, 5, InstructionCores.Cmp, true, indexedDummyReads);

        Read(table, 0xE0, "CPX", AddrMode.I, 2, 2, InstructionCores.Cpx, false, indexedDummyReads);
        Read(table, 0xE4, "CPX", AddrMode.ZP, 2, 3, InstructionCores.Cpx, false, indexedDummyReads);
        Read(table, 0xEC, "CPX", AddrMode.ABS, 3, 4, InstructionCores.Cpx, false, indexedDummyReads);
        Read(table, 0xC0, "CPY", AddrMode.I, 2, 2, InstructionCores.Cpy, false, indexedDummyReads);
        Read(table, 0xC4, "CPY", AddrMode.ZP, 2, 3, InstructionCores.Cpy, false, indexedDummyReads);
        Read(table, 0xCC, "CPY", AddrMode.ABS, 3, 4, InstructionCores.Cpy, false, indexedDummyReads);

        // --- BIT (the modes shared by both models; 65C02-only modes stay bespoke) ---
        Read(table, 0x24, "BIT", AddrMode.ZP, 2, 3, InstructionCores.Bit, false, indexedDummyReads);
        Read(table, 0x2C, "BIT", AddrMode.ABS, 3, 4, InstructionCores.Bit, false, indexedDummyReads);

        // --- Register increment/decrement ---
        Implied(table, 0xE8, "INX", 2, InstructionCores.Inx);
        Implied(table, 0xC8, "INY", 2, InstructionCores.Iny);
        Implied(table, 0xCA, "DEX", 2, InstructionCores.Dex);
        Implied(table, 0x88, "DEY", 2, InstructionCores.Dey);

        // --- Branches (2 cycles, +1 taken, +1 page cross) ---
        Branch(table, 0x10, "BPL", static cpu => !cpu.ProcessorStatus.Negative);
        Branch(table, 0x30, "BMI", static cpu => cpu.ProcessorStatus.Negative);
        Branch(table, 0x50, "BVC", static cpu => !cpu.ProcessorStatus.Overflow);
        Branch(table, 0x70, "BVS", static cpu => cpu.ProcessorStatus.Overflow);
        Branch(table, 0x90, "BCC", static cpu => !cpu.ProcessorStatus.Carry);
        Branch(table, 0xB0, "BCS", static cpu => cpu.ProcessorStatus.Carry);
        Branch(table, 0xD0, "BNE", static cpu => !cpu.ProcessorStatus.Zero);
        Branch(table, 0xF0, "BEQ", static cpu => cpu.ProcessorStatus.Zero);

        // --- Flag operations ---
        Implied(table, 0x18, "CLC", 2, InstructionCores.Clc);
        Implied(table, 0x38, "SEC", 2, InstructionCores.Sec);
        Implied(table, 0x58, "CLI", 2, InstructionCores.Cli);
        Implied(table, 0x78, "SEI", 2, InstructionCores.Sei);
        Implied(table, 0xB8, "CLV", 2, InstructionCores.Clv);
        Implied(table, 0xD8, "CLD", 2, InstructionCores.Cld);
        Implied(table, 0xF8, "SED", 2, InstructionCores.Sed);

        // --- Stack, flow, NOP (bespoke handlers; cycles returned by the handler) ---
        Bespoke(table, 0x48, "PHA", AddrMode.Implied, 1, 3, SharedHandlers.Pha);
        Bespoke(table, 0x68, "PLA", AddrMode.Implied, 1, 4, SharedHandlers.Pla);
        Bespoke(table, 0x08, "PHP", AddrMode.Implied, 1, 3, SharedHandlers.Php);
        Bespoke(table, 0x28, "PLP", AddrMode.Implied, 1, 4, SharedHandlers.Plp);
        Bespoke(table, 0x4C, "JMP", AddrMode.ABS, 3, 3, SharedHandlers.Jmp_Absolute);
        Bespoke(table, 0x20, "JSR", AddrMode.ABS, 3, 6, SharedHandlers.Jsr);
        Bespoke(table, 0x60, "RTS", AddrMode.Implied, 1, 6, SharedHandlers.Rts);
        Bespoke(table, 0x40, "RTI", AddrMode.Implied, 1, 6, SharedHandlers.Rti);
        Bespoke(table, 0xEA, "NOP", AddrMode.Implied, 1, 2, SharedHandlers.Nop);
        Bespoke(table, 0x00, "BRK", AddrMode.Implied, 1, 7, SharedHandlers.Brk);
    }

    /// <summary>
    /// ADC/SBC bindings for a model, with the model's decimal-mode core (NMOS flags vs
    /// 65C02 valid flags + extra cycle). The $EB undocumented SBC alias is NOT bound here
    /// — it is profile-dependent and migrates with the illegal-opcode group.
    /// </summary>
    public static void ApplyAdcSbc(OpCodeDescriptor?[] table, ReadOperation adcCore, ReadOperation sbcCore, bool indexedDummyReads)
    {
        Read(table, 0x69, "ADC", AddrMode.I, 2, 2, adcCore, false, indexedDummyReads);
        Read(table, 0x65, "ADC", AddrMode.ZP, 2, 3, adcCore, false, indexedDummyReads);
        Read(table, 0x75, "ADC", AddrMode.ZP_X, 2, 4, adcCore, false, indexedDummyReads);
        Read(table, 0x6D, "ADC", AddrMode.ABS, 3, 4, adcCore, false, indexedDummyReads);
        Read(table, 0x7D, "ADC", AddrMode.ABS_X, 3, 4, adcCore, true, indexedDummyReads);
        Read(table, 0x79, "ADC", AddrMode.ABS_Y, 3, 4, adcCore, true, indexedDummyReads);
        Read(table, 0x61, "ADC", AddrMode.IX_IND, 2, 6, adcCore, false, indexedDummyReads);
        Read(table, 0x71, "ADC", AddrMode.IND_IX, 2, 5, adcCore, true, indexedDummyReads);

        Read(table, 0xE9, "SBC", AddrMode.I, 2, 2, sbcCore, false, indexedDummyReads);
        Read(table, 0xE5, "SBC", AddrMode.ZP, 2, 3, sbcCore, false, indexedDummyReads);
        Read(table, 0xF5, "SBC", AddrMode.ZP_X, 2, 4, sbcCore, false, indexedDummyReads);
        Read(table, 0xED, "SBC", AddrMode.ABS, 3, 4, sbcCore, false, indexedDummyReads);
        Read(table, 0xFD, "SBC", AddrMode.ABS_X, 3, 4, sbcCore, true, indexedDummyReads);
        Read(table, 0xF9, "SBC", AddrMode.ABS_Y, 3, 4, sbcCore, true, indexedDummyReads);
        Read(table, 0xE1, "SBC", AddrMode.IX_IND, 2, 6, sbcCore, false, indexedDummyReads);
        Read(table, 0xF1, "SBC", AddrMode.IND_IX, 2, 5, sbcCore, true, indexedDummyReads);
    }

    /// <summary>
    /// Shift/rotate and memory INC/DEC bindings for a model. The bus sequence is per
    /// model (<paramref name="cmosSequence"/>): NMOS read-write-write, 65C02
    /// read-read-write. abs,X cycles differ too: NMOS always 7; 65C02 shifts/rotates
    /// 6 + 1 on page cross, 65C02 INC/DEC always 7.
    /// </summary>
    public static void ApplyRmw(OpCodeDescriptor?[] table, bool cmosSequence, bool indexedDummyReads)
    {
        Implied(table, 0x0A, "ASL", 2, InstructionCores.AslAccumulator, AddrMode.Accumulator);
        Implied(table, 0x4A, "LSR", 2, InstructionCores.LsrAccumulator, AddrMode.Accumulator);
        Implied(table, 0x2A, "ROL", 2, InstructionCores.RolAccumulator, AddrMode.Accumulator);
        Implied(table, 0x6A, "ROR", 2, InstructionCores.RorAccumulator, AddrMode.Accumulator);

        var shiftAbsXBaseCycles = (byte)(cmosSequence ? 6 : 7);
        var shiftAbsXAddsPageCross = cmosSequence;

        Rmw(table, 0x06, "ASL", AddrMode.ZP, 2, 5, InstructionCores.Asl, cmosSequence, indexedDummyReads);
        Rmw(table, 0x16, "ASL", AddrMode.ZP_X, 2, 6, InstructionCores.Asl, cmosSequence, indexedDummyReads);
        Rmw(table, 0x0E, "ASL", AddrMode.ABS, 3, 6, InstructionCores.Asl, cmosSequence, indexedDummyReads);
        Rmw(table, 0x1E, "ASL", AddrMode.ABS_X, 3, shiftAbsXBaseCycles, InstructionCores.Asl, cmosSequence, indexedDummyReads, shiftAbsXAddsPageCross);

        Rmw(table, 0x46, "LSR", AddrMode.ZP, 2, 5, InstructionCores.Lsr, cmosSequence, indexedDummyReads);
        Rmw(table, 0x56, "LSR", AddrMode.ZP_X, 2, 6, InstructionCores.Lsr, cmosSequence, indexedDummyReads);
        Rmw(table, 0x4E, "LSR", AddrMode.ABS, 3, 6, InstructionCores.Lsr, cmosSequence, indexedDummyReads);
        Rmw(table, 0x5E, "LSR", AddrMode.ABS_X, 3, shiftAbsXBaseCycles, InstructionCores.Lsr, cmosSequence, indexedDummyReads, shiftAbsXAddsPageCross);

        Rmw(table, 0x26, "ROL", AddrMode.ZP, 2, 5, InstructionCores.Rol, cmosSequence, indexedDummyReads);
        Rmw(table, 0x36, "ROL", AddrMode.ZP_X, 2, 6, InstructionCores.Rol, cmosSequence, indexedDummyReads);
        Rmw(table, 0x2E, "ROL", AddrMode.ABS, 3, 6, InstructionCores.Rol, cmosSequence, indexedDummyReads);
        Rmw(table, 0x3E, "ROL", AddrMode.ABS_X, 3, shiftAbsXBaseCycles, InstructionCores.Rol, cmosSequence, indexedDummyReads, shiftAbsXAddsPageCross);

        Rmw(table, 0x66, "ROR", AddrMode.ZP, 2, 5, InstructionCores.Ror, cmosSequence, indexedDummyReads);
        Rmw(table, 0x76, "ROR", AddrMode.ZP_X, 2, 6, InstructionCores.Ror, cmosSequence, indexedDummyReads);
        Rmw(table, 0x6E, "ROR", AddrMode.ABS, 3, 6, InstructionCores.Ror, cmosSequence, indexedDummyReads);
        Rmw(table, 0x7E, "ROR", AddrMode.ABS_X, 3, shiftAbsXBaseCycles, InstructionCores.Ror, cmosSequence, indexedDummyReads, shiftAbsXAddsPageCross);

        Rmw(table, 0xE6, "INC", AddrMode.ZP, 2, 5, InstructionCores.Inc, cmosSequence, indexedDummyReads);
        Rmw(table, 0xF6, "INC", AddrMode.ZP_X, 2, 6, InstructionCores.Inc, cmosSequence, indexedDummyReads);
        Rmw(table, 0xEE, "INC", AddrMode.ABS, 3, 6, InstructionCores.Inc, cmosSequence, indexedDummyReads);
        Rmw(table, 0xFE, "INC", AddrMode.ABS_X, 3, 7, InstructionCores.Inc, cmosSequence, indexedDummyReads);

        Rmw(table, 0xC6, "DEC", AddrMode.ZP, 2, 5, InstructionCores.Dec, cmosSequence, indexedDummyReads);
        Rmw(table, 0xD6, "DEC", AddrMode.ZP_X, 2, 6, InstructionCores.Dec, cmosSequence, indexedDummyReads);
        Rmw(table, 0xCE, "DEC", AddrMode.ABS, 3, 6, InstructionCores.Dec, cmosSequence, indexedDummyReads);
        Rmw(table, 0xDE, "DEC", AddrMode.ABS_X, 3, 7, InstructionCores.Dec, cmosSequence, indexedDummyReads);
    }

    /// <summary>
    /// The 65C02-only instructions bindable from cores/composition: TSB/TRB,
    /// INC A/DEC A, STZ, the new BIT modes, and BRA. (PHX/PHY/PLX/PLY and the
    /// model-specific JMP handlers stay as bespoke statics in CmosHandlers.)
    /// </summary>
    public static void ApplyCmosExtras(OpCodeDescriptor?[] table)
    {
        Implied(table, 0x1A, "INC", 2, InstructionCores.IncAccumulator, AddrMode.Accumulator);
        Implied(table, 0x3A, "DEC", 2, InstructionCores.DecAccumulator, AddrMode.Accumulator);
        Rmw(table, 0x04, "TSB", AddrMode.ZP, 2, 5, InstructionCores.Tsb, cmosSequence: true, indexedDummyReads: false);
        Rmw(table, 0x0C, "TSB", AddrMode.ABS, 3, 6, InstructionCores.Tsb, cmosSequence: true, indexedDummyReads: false);
        Rmw(table, 0x14, "TRB", AddrMode.ZP, 2, 5, InstructionCores.Trb, cmosSequence: true, indexedDummyReads: false);
        Rmw(table, 0x1C, "TRB", AddrMode.ABS, 3, 6, InstructionCores.Trb, cmosSequence: true, indexedDummyReads: false);

        Store(table, 0x64, "STZ", AddrMode.ZP, 2, 3, InstructionCores.Stz, indexedDummyReads: false);
        Store(table, 0x74, "STZ", AddrMode.ZP_X, 2, 4, InstructionCores.Stz, indexedDummyReads: false);
        Store(table, 0x9C, "STZ", AddrMode.ABS, 3, 4, InstructionCores.Stz, indexedDummyReads: false);     // SHY abs,X on NMOS
        Store(table, 0x9E, "STZ", AddrMode.ABS_X, 3, 5, InstructionCores.Stz, indexedDummyReads: false);   // SHX abs,Y on NMOS

        Read(table, 0x89, "BIT", AddrMode.I, 2, 2, InstructionCores.BitImmediateCmos, false, indexedDummyReads: false);
        Read(table, 0x34, "BIT", AddrMode.ZP_X, 2, 4, InstructionCores.Bit, false, indexedDummyReads: false);
        Read(table, 0x3C, "BIT", AddrMode.ABS_X, 3, 4, InstructionCores.Bit, true, indexedDummyReads: false);

        Branch(table, 0x80, "BRA", static _ => true);
    }

    private static void Branch(OpCodeDescriptor?[] table, byte code, string mnemonic, BranchCondition condition)
        => table[code] = new OpCodeDescriptor
        {
            Code = code,
            Mnemonic = mnemonic,
            Addressing = AddrMode.Relative,
            Size = 2,
            BaseCycles = 2,
            Documented = true,
            Execute = ComposeBranch(condition),
        };

    private static void Bespoke(OpCodeDescriptor?[] table, byte code, string mnemonic, AddrMode addressing,
        byte size, byte baseCycles, ExecuteHandler handler)
        => table[code] = new OpCodeDescriptor
        {
            Code = code,
            Mnemonic = mnemonic,
            Addressing = addressing,
            Size = size,
            BaseCycles = baseCycles,
            Documented = true,
            Execute = handler,
        };

    private static void Rmw(OpCodeDescriptor?[] table, byte code, string mnemonic, AddrMode addressing,
        byte size, byte baseCycles, RmwOperation core, bool cmosSequence, bool indexedDummyReads, bool addPageCrossCycle = false)
        => table[code] = new OpCodeDescriptor
        {
            Code = code,
            Mnemonic = mnemonic,
            Addressing = addressing,
            Size = size,
            BaseCycles = baseCycles,
            Documented = true,
            Execute = ComposeRmw(addressing, baseCycles, core, cmosSequence, indexedDummyReads, addPageCrossCycle),
        };

    /// <summary>
    /// The 65C02's "(zp)" zero-page-indirect forms, bound from the same cores as the
    /// instructions' other modes (CMOS cores for ADC/SBC). 2 bytes, 5 cycles each.
    /// </summary>
    public static void ApplyCmosZpIndirectForms(OpCodeDescriptor?[] table)
    {
        Read(table, 0x12, "ORA", AddrMode.ZP_IND, 2, 5, InstructionCores.Ora, false, indexedDummyReads: false);
        Read(table, 0x32, "AND", AddrMode.ZP_IND, 2, 5, InstructionCores.And, false, indexedDummyReads: false);
        Read(table, 0x52, "EOR", AddrMode.ZP_IND, 2, 5, InstructionCores.Eor, false, indexedDummyReads: false);
        Read(table, 0x72, "ADC", AddrMode.ZP_IND, 2, 5, InstructionCores.AdcCmos, false, indexedDummyReads: false);
        Store(table, 0x92, "STA", AddrMode.ZP_IND, 2, 5, InstructionCores.Sta, indexedDummyReads: false);
        Read(table, 0xB2, "LDA", AddrMode.ZP_IND, 2, 5, InstructionCores.Lda, false, indexedDummyReads: false);
        Read(table, 0xD2, "CMP", AddrMode.ZP_IND, 2, 5, InstructionCores.Cmp, false, indexedDummyReads: false);
        Read(table, 0xF2, "SBC", AddrMode.ZP_IND, 2, 5, InstructionCores.SbcCmos, false, indexedDummyReads: false);
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
        byte baseCycles, ImpliedOperation core, AddrMode addressing = AddrMode.Implied)
        => table[code] = new OpCodeDescriptor
        {
            Code = code,
            Mnemonic = mnemonic,
            Addressing = addressing,
            Size = 1,
            BaseCycles = baseCycles,
            Documented = true,
            Execute = ComposeImplied(baseCycles, core),
        };
}
