using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2.TestRom;

/// <summary>
/// A home-made 12 KB Apple II ROM image that exercises the whole machine without needing the
/// real (copyrighted) Applesoft + Autostart ROM: it boots through the reset vector, clears the
/// text page, writes a distinct character to column 0 of every one of the 24 rows through the
/// interleaved row-base table, then polls the keyboard soft switches and echoes typed
/// characters — cycling normal, inverse and flashing video — into the bottom row.
///
/// Proves, end to end: reset vector and ROM mapping, RAM, zero-page indirect addressing, the
/// interleaved text layout, the $C000 / $C010 keyboard soft switches, and the character
/// encoding — everything except Applesoft itself.
/// </summary>
internal static class Apple2SyntheticRom
{
    public const ushort ProgramStartAddress = 0xF800;

    /// <summary>Row that the keyboard echo writes into.</summary>
    public const int EchoRow = 23;

    /// <summary>First column of the keyboard echo; column 0 holds the row banner character.</summary>
    public const int EchoFirstColumn = 1;

    /// <summary>Screen byte written to column 0 of row 0 by the banner loop ('A' in normal video).</summary>
    public const byte BannerFirstScreenByte = 0xC1;

    // Zero page scratch used by the program.
    private const byte ZpScreenPointerLo = 0x06;
    private const byte ZpScreenPointerHi = 0x07;
    private const byte ZpColumn = 0x08;
    private const byte ZpAttributeIndex = 0x09;

    // Opcodes used below.
    private const byte OpCld = 0xD8;
    private const byte OpLdxImm = 0xA2;
    private const byte OpTxs = 0x9A;
    private const byte OpLdaImm = 0xA9;
    private const byte OpStaAbsX = 0x9D;
    private const byte OpInx = 0xE8;
    private const byte OpBne = 0xD0;
    private const byte OpLdaAbsX = 0xBD;
    private const byte OpStaZp = 0x85;
    private const byte OpTxa = 0x8A;
    private const byte OpClc = 0x18;
    private const byte OpAdcImm = 0x69;
    private const byte OpLdyImm = 0xA0;
    private const byte OpStaIndY = 0x91;
    private const byte OpCpxImm = 0xE0;
    private const byte OpLdaAbs = 0xAD;
    private const byte OpBpl = 0x10;
    private const byte OpBitAbs = 0x2C;
    private const byte OpAndImm = 0x29;
    private const byte OpLdxZp = 0xA6;
    private const byte OpBeq = 0xF0;
    private const byte OpDex = 0xCA;
    private const byte OpOraImm = 0x09;
    private const byte OpJmpAbs = 0x4C;
    private const byte OpLdyZp = 0xA4;
    private const byte OpStxZp = 0x86;
    private const byte OpIncZp = 0xE6;
    private const byte OpLdaZp = 0xA5;
    private const byte OpCmpImm = 0xC9;

    /// <summary>Builds the 12 KB image that maps at $D000-$FFFF.</summary>
    public static byte[] Build()
    {
        var code = AssembleProgram();

        var rom = new byte[Apple2System.SystemRomSize];
        Array.Fill(rom, (byte)0xEA);   // NOP filler

        var programOffset = ProgramStartAddress - Apple2System.SystemRomStartAddress;
        code.CopyTo(rom, programOffset);

        // Reset vector at $FFFC/$FFFD.
        var resetVectorOffset = 0xFFFC - Apple2System.SystemRomStartAddress;
        rom[resetVectorOffset] = (byte)(ProgramStartAddress & 0xFF);
        rom[resetVectorOffset + 1] = (byte)(ProgramStartAddress >> 8);

        return rom;
    }

    private static byte[] AssembleProgram()
    {
        var asm = new RomAssembler(ProgramStartAddress);

        //          CLD
        //          LDX #$FF
        //          TXS
        asm.Emit(OpCld);
        asm.Emit(OpLdxImm, 0xFF);
        asm.Emit(OpTxs);

        // ---- Clear text page 1 with normal-video spaces ($A0).
        //          LDA #$A0
        //          LDX #$00
        // clear:   STA $0400,X / $0500,X / $0600,X / $0700,X
        //          INX
        //          BNE clear
        asm.Emit(OpLdaImm, 0xA0);
        asm.Emit(OpLdxImm, 0x00);
        asm.Label("clear");
        asm.Emit(OpStaAbsX, 0x00, 0x04);
        asm.Emit(OpStaAbsX, 0x00, 0x05);
        asm.Emit(OpStaAbsX, 0x00, 0x06);
        asm.Emit(OpStaAbsX, 0x00, 0x07);
        asm.Emit(OpInx);
        asm.EmitBranch(OpBne, "clear");

        // ---- Banner: write ('A' + row) to column 0 of each row, via the row-base table.
        //          LDX #$00
        // banner:  LDA rowLo,X / STA $06
        //          LDA rowHi,X / STA $07
        //          TXA / CLC / ADC #$C1
        //          LDY #$00 / STA ($06),Y
        //          INX / CPX #24 / BNE banner
        asm.Emit(OpLdxImm, 0x00);
        asm.Label("banner");
        asm.EmitAbsolute(OpLdaAbsX, "rowLo");
        asm.Emit(OpStaZp, ZpScreenPointerLo);
        asm.EmitAbsolute(OpLdaAbsX, "rowHi");
        asm.Emit(OpStaZp, ZpScreenPointerHi);
        asm.Emit(OpTxa);
        asm.Emit(OpClc);
        asm.Emit(OpAdcImm, BannerFirstScreenByte);
        asm.Emit(OpLdyImm, 0x00);
        asm.Emit(OpStaIndY, ZpScreenPointerLo);
        asm.Emit(OpInx);
        asm.Emit(OpCpxImm, (byte)Apple2TextScreen.Rows);
        asm.EmitBranch(OpBne, "banner");

        // ---- Point the echo cursor at the bottom row, first echo column.
        asm.EmitAbsolute(OpLdaAbs, "rowLo", EchoRow);
        asm.Emit(OpStaZp, ZpScreenPointerLo);
        asm.EmitAbsolute(OpLdaAbs, "rowHi", EchoRow);
        asm.Emit(OpStaZp, ZpScreenPointerHi);
        asm.Emit(OpLdaImm, EchoFirstColumn);
        asm.Emit(OpStaZp, ZpColumn);
        asm.Emit(OpLdaImm, 0x00);
        asm.Emit(OpStaZp, ZpAttributeIndex);

        // ---- Keyboard echo loop.
        // kbd:     LDA $C000 / BPL kbd          ; wait for the strobe
        //          BIT $C010                    ; clear the strobe
        //          AND #$3F                     ; ASCII -> character-generator glyph index
        //          LDX $09 / BEQ normal         ; attribute 0 = normal
        //          DEX / BEQ store              ; attribute 1 = inverse (glyph index as-is)
        //          ORA #$40 / JMP store         ; attribute 2 = flashing
        // normal:  ORA #$80
        // store:   LDY $08 / STA ($06),Y
        asm.Label("kbd");
        asm.Emit(OpLdaAbs, (byte)(Apple2SoftSwitches.KeyboardDataAddress & 0xFF), (byte)(Apple2SoftSwitches.KeyboardDataAddress >> 8));
        asm.EmitBranch(OpBpl, "kbd");
        asm.Emit(OpBitAbs, (byte)(Apple2SoftSwitches.KeyboardStrobeClearAddress & 0xFF), (byte)(Apple2SoftSwitches.KeyboardStrobeClearAddress >> 8));
        asm.Emit(OpAndImm, 0x3F);
        asm.Emit(OpLdxZp, ZpAttributeIndex);
        asm.EmitBranch(OpBeq, "normal");
        asm.Emit(OpDex);
        asm.EmitBranch(OpBeq, "store");
        asm.Emit(OpOraImm, 0x40);
        asm.EmitAbsolute(OpJmpAbs, "store");
        asm.Label("normal");
        asm.Emit(OpOraImm, 0x80);
        asm.Label("store");
        asm.Emit(OpLdyZp, ZpColumn);
        asm.Emit(OpStaIndY, ZpScreenPointerLo);

        // ---- Advance the attribute index 0 -> 1 -> 2 -> 0.
        asm.Emit(OpLdxZp, ZpAttributeIndex);
        asm.Emit(OpInx);
        asm.Emit(OpCpxImm, 0x03);
        asm.EmitBranch(OpBne, "saveAttribute");
        asm.Emit(OpLdxImm, 0x00);
        asm.Label("saveAttribute");
        asm.Emit(OpStxZp, ZpAttributeIndex);

        // ---- Advance the column, wrapping back to the first echo column.
        asm.Emit(OpIncZp, ZpColumn);
        asm.Emit(OpLdaZp, ZpColumn);
        asm.Emit(OpCmpImm, (byte)Apple2TextScreen.Columns);
        asm.EmitBranch(OpBne, "kbd");
        asm.Emit(OpLdaImm, EchoFirstColumn);
        asm.Emit(OpStaZp, ZpColumn);
        asm.EmitAbsolute(OpJmpAbs, "kbd");

        // ---- Row-base lookup tables (the interleaved text layout).
        asm.Label("rowLo");
        for (var row = 0; row < Apple2TextScreen.Rows; row++)
            asm.Emit((byte)(Apple2TextScreen.GetRowStartAddress(row) & 0xFF));
        asm.Label("rowHi");
        for (var row = 0; row < Apple2TextScreen.Rows; row++)
            asm.Emit((byte)(Apple2TextScreen.GetRowStartAddress(row) >> 8));

        return asm.Assemble();
    }
}
