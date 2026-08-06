namespace Highbyte.DotNet6502.Systems.Apple2.Video;

/// <summary>
/// Apple II hi-res page addressing.
///
/// Like the text page, hi-res scan lines are not laid out linearly. A line's offset from the
/// page base is <c>(y % 8) * $400 + ((y / 8) % 8) * $80 + (y / 64) * $28</c>: consecutive
/// scan lines of a character-cell row sit $400 apart, cell rows within a band sit $80 apart,
/// and the three 64-line bands sit $28 apart. Each 128-byte block ends with 8 unused
/// "screen hole" bytes, exactly as on the text page.
///
/// Each of the 40 bytes on a line carries 7 pixels in bits 0-6, bit 0 leftmost. Bit 7 is the
/// NTSC color-shift bit and does not affect which pixels are lit.
/// </summary>
public static class Apple2HiResScreen
{
    public const int BytesPerLine = 40;
    public const int Lines = 192;
    public const int PixelsPerByte = 7;

    /// <summary>Hi-res page 1 base address ($2000-$3FFF).</summary>
    public const ushort HiResPage1BaseAddress = 0x2000;

    /// <summary>Hi-res page 2 base address ($4000-$5FFF).</summary>
    public const ushort HiResPage2BaseAddress = 0x4000;

    /// <summary>Size of a hi-res page, including the unused screen-hole bytes.</summary>
    public const int HiResPageSize = 0x2000;

    private static readonly ushort[] s_lineOffsets = BuildLineOffsets();

    private static ushort[] BuildLineOffsets()
    {
        var offsets = new ushort[Lines];
        for (var y = 0; y < Lines; y++)
            offsets[y] = (ushort)(((y % 8) * 0x400) + (((y / 8) % 8) * 0x80) + ((y / 64) * 0x28));
        return offsets;
    }

    /// <summary>Offset of a scan line from the start of its hi-res page.</summary>
    public static ushort GetLineOffset(int y)
    {
        ValidateLine(y);
        return s_lineOffsets[y];
    }

    /// <summary>Address of the first byte of a scan line.</summary>
    public static ushort GetLineStartAddress(int y, ushort pageBaseAddress = HiResPage1BaseAddress)
        => (ushort)(pageBaseAddress + GetLineOffset(y));

    private static void ValidateLine(int y)
    {
        if (y < 0 || y >= Lines)
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Hi-res scan line must be 0-{Lines - 1}.");
    }
}
