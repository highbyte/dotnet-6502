namespace Highbyte.DotNet6502.Systems.Apple2.Video;

/// <summary>Display attribute encoded in bits 7-6 of an Apple II text screen byte.</summary>
public enum Apple2TextAttribute
{
    /// <summary>$00-$3F — foreground and background swapped.</summary>
    Inverse,
    /// <summary>$40-$7F — alternates between normal and inverse at roughly 2 Hz.</summary>
    Flash,
    /// <summary>$80-$FF — normal video.</summary>
    Normal,
}

/// <summary>
/// Apple II text character encoding.
///
/// The character generator (a 2513 ROM) holds 64 glyphs in a fixed order: the first 32 are
/// <c>@ A-Z [ \ ] ^ _</c> (ASCII $40-$5F) and the next 32 are <c>space ! " # ...  ?</c>
/// (ASCII $20-$3F). The video circuitry selects the glyph with bits 5-0 of the screen byte and
/// uses bits 7-6 only for the inverse/flash attribute — which is why an Apple II or II Plus
/// shows the same 64 glyphs twice across the normal-video range $80-$FF and cannot display
/// lowercase.
/// </summary>
public static class Apple2CharSet
{
    /// <summary>Number of glyphs in the character generator.</summary>
    public const int GlyphCount = 64;

    /// <summary>Scan lines stored per glyph in the character generator ROM.</summary>
    public const int GlyphRowCount = 8;

    /// <summary>Bytes of a character generator ROM image that hold the 64 glyph patterns.</summary>
    public const int CharacterRomSize = GlyphCount * GlyphRowCount;

    /// <summary>Dot columns the 2513 actually stores per scan line (the cell adds 2 blank columns).</summary>
    public const int GlyphDotWidth = 5;

    /// <summary>Bit position of the leftmost dot in a character generator scan-line byte.</summary>
    public const int GlyphDotShift = 5;

    /// <summary>Attribute encoded in bits 7-6 of a screen byte.</summary>
    public static Apple2TextAttribute GetAttribute(byte screenByte) => (screenByte & 0xC0) switch
    {
        0x00 => Apple2TextAttribute.Inverse,
        0x40 => Apple2TextAttribute.Flash,
        _ => Apple2TextAttribute.Normal,
    };

    /// <summary>Character-generator glyph index (0-63) of a screen byte.</summary>
    public static byte GetGlyphIndex(byte screenByte) => (byte)(screenByte & 0x3F);

    /// <summary>ASCII code of a character-generator glyph index.</summary>
    public static byte GlyphIndexToAscii(byte glyphIndex)
    {
        var index = (byte)(glyphIndex & 0x3F);
        return index < 0x20 ? (byte)(index + 0x40) : index;
    }

    /// <summary>ASCII code displayed by a screen byte, ignoring its attribute.</summary>
    public static byte ToAscii(byte screenByte) => GlyphIndexToAscii(GetGlyphIndex(screenByte));

    /// <summary>
    /// Screen byte that displays an ASCII character. Lowercase is folded to uppercase because
    /// the Apple II / II Plus character generator has no lowercase glyphs.
    /// </summary>
    public static byte FromAscii(byte ascii, Apple2TextAttribute attribute = Apple2TextAttribute.Normal)
    {
        var code = (byte)(ascii & 0x7F);
        if (code >= 0x61 && code <= 0x7A)
            code -= 0x20;                       // lowercase → uppercase

        return attribute switch
        {
            // Inverse and flash only have the 64 glyph slots to address.
            Apple2TextAttribute.Inverse => (byte)(code & 0x3F),
            Apple2TextAttribute.Flash => (byte)((code & 0x3F) | 0x40),
            // Normal video is plain high-bit ASCII — what Applesoft and the Monitor write.
            _ => (byte)(code | 0x80),
        };
    }

    /// <summary>
    /// Glyph-to-text conversion handed to render targets via the
    /// <c>SetConfig</c> video command. The screen byte is passed through as the glyph id, so
    /// this maps it to the single character the character generator would draw.
    /// </summary>
    public static string ScreenCodeToUnicode(byte screenByte) => ((char)ToAscii(screenByte)).ToString();
}
