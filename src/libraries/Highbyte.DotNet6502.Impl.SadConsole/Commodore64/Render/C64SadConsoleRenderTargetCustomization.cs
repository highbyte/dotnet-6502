using Highbyte.DotNet6502.Systems.Commodore64;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Render;
public class C64SadConsoleRenderTargetCustomization
{
    private readonly C64 _c64;

    public C64SadConsoleRenderTargetCustomization(C64 c64)
    {
        _c64 = c64;
    }

    public (int tranformedCharacter, Color transformedFgColor, Color transformedBgColor) TransformCharacterAndColor(
        int emulatorCharacter,
        Color fgColor,
        Color bgColor)
    {
        var tranformedCharacter = TranslateC64ScreenCodeToSadConsoleC64ROMFontIndex((byte)emulatorCharacter, out var inverted);
        Color transformedFgColor;
        Color transformedBgColor;
        if (inverted)
        {
            transformedFgColor = bgColor;
            transformedBgColor = fgColor;
        }
        else
        {
            transformedFgColor = fgColor;
            transformedBgColor = bgColor;
        }
        return (tranformedCharacter, transformedFgColor, transformedBgColor);
    }

    private byte TranslateC64ScreenCodeToSadConsoleC64ROMFontIndex(byte sourceByte, out bool inverted)
    {
        // The custom SadConsole font C64_ROM.font:
        // - is a combination of the two built-in C64 ROM fonts, the non-inverted parts of "shifted" and "unshifted" ROM fonts.
        // - the first 128 characters from the shifted font (non-inverted) are index 0-127.
        // - the first 128 characters from the unshufter font (non-inverted) are index 1-255.
        // - Other changes:
        //   - A sad console font must have index 0 as empty (only transparent background). As the C64 font has a @ sign in this position, it has been removed.
        //   - A sad console font must have one index as a solid block (only forground). As the C64 non-inverted parts of the C64 ROM fons does not contain a solid block, a duplicate of an empty block (160) has been changed to contain a solid block, one of the duplicate empty characters has been replace with a solid block.
        //   - These differences, as well as detecting inverted characters (and swapping foreground/background color when drawing) are mapped via code.


        // ----------
        // Mapping general C64 font -> SadConsole C64_ROM.font font translation
        // ----------
        byte sadConsoleGlyphIndex;
        var isUnshiftedC64CharRom = _c64.Vic2.CharsetManager.CharacterSetAddressInVIC2BankIsChargenROMUnshifted;
        if (isUnshiftedC64CharRom)
        {
            if (sourceByte < 128)
                sadConsoleGlyphIndex = (byte)(sourceByte + 128);
            else
                sadConsoleGlyphIndex = sourceByte;
        }
        else
        {
            if (sourceByte >= 128)
                sadConsoleGlyphIndex = (byte)(sourceByte - 128);
            else
                sadConsoleGlyphIndex = sourceByte;
        }

        // ----------
        // Mapping exceptions between C64 font -> SadConsole C64_ROM.font font
        // ----------
        // The first glyph in a SadConsole font must be empty (where the C64 ROM font has a @ sign).
        // The @ sign has been removed from the C64 ROM font. The @ sign has a duplicate in position 128.
        if (sourceByte == 0)
        {
            sadConsoleGlyphIndex = 128;
            inverted = false;
        }
        // One glyph in SadConsole font must contain a solid block. As the combined font from C64 shifted and unshifted (with both inverted removed),
        // a duplicate of an empty block (160) has been changed to contain a solid block.
        // To avoid Space (32) when using an unshifted C64 Char ROM (that would add 128 to the index of the combined SadConsole font) to
        // be the solid block (see below), always use Space (32).
        else if (sourceByte == 32)
        {
            sadConsoleGlyphIndex = 32;
            inverted = false;
        }
        // One glyph in SadConsole font must contain a solid block. As the combined font from C64 shifted and unshifted (with both inverted removed),
        // a duplicate of an empty block (160) has been changed to contain a solid block.
        // In the original C64 font (both shifted and unshifted) this position was a solid block. So use it, and tell not to invert it.
        else if (sourceByte == 160)
        {
            sadConsoleGlyphIndex = 160;
            inverted = false;
        }
        else
        {
            inverted = sourceByte >= 128;
        }

        return sadConsoleGlyphIndex;
    }

}
