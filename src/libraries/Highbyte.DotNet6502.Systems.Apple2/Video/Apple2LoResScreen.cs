using System.Drawing;

namespace Highbyte.DotNet6502.Systems.Apple2.Video;

/// <summary>
/// Apple II lo-res (GR) geometry and colors.
///
/// Lo-res reinterprets the active <em>text</em> page: each of the 40x24 screen bytes holds two
/// vertically stacked 4-bit color blocks — the low nibble is the upper block (even lo-res row),
/// the high nibble the lower block (odd row) — giving 40x48 blocks of 7x4 display pixels.
/// </summary>
public static class Apple2LoResScreen
{
    public const int BlockColumns = Apple2TextScreen.Columns;      // 40
    public const int BlockRows = Apple2TextScreen.Rows * 2;        // 48
    public const int BlockPixelWidth = 7;
    public const int BlockPixelHeight = 4;

    /// <summary>
    /// The 16 lo-res colors, indexed by nibble value. Simplified composite palette (the common
    /// NTSC-derived RGB approximation); colors 5 and 10 are the same grey on real hardware too.
    /// </summary>
    public static readonly Color[] Palette =
    {
        Color.FromArgb(255, 0, 0, 0),         //  0 black
        Color.FromArgb(255, 227, 30, 96),     //  1 magenta
        Color.FromArgb(255, 96, 78, 189),     //  2 dark blue
        Color.FromArgb(255, 255, 68, 253),    //  3 purple
        Color.FromArgb(255, 0, 163, 96),      //  4 dark green
        Color.FromArgb(255, 156, 156, 156),   //  5 grey 1
        Color.FromArgb(255, 20, 207, 253),    //  6 medium blue
        Color.FromArgb(255, 208, 195, 255),   //  7 light blue
        Color.FromArgb(255, 96, 114, 3),      //  8 brown
        Color.FromArgb(255, 255, 106, 60),    //  9 orange
        Color.FromArgb(255, 156, 156, 156),   // 10 grey 2
        Color.FromArgb(255, 255, 160, 208),   // 11 pink
        Color.FromArgb(255, 20, 245, 60),     // 12 light green
        Color.FromArgb(255, 208, 221, 141),   // 13 yellow
        Color.FromArgb(255, 114, 255, 208),   // 14 aquamarine
        Color.FromArgb(255, 255, 255, 255),   // 15 white
    };

    /// <summary>Color index of one of the two blocks in a screen byte.</summary>
    public static int GetColorIndex(byte screenByte, bool upperBlock)
        => upperBlock ? screenByte & 0x0F : (screenByte >> 4) & 0x0F;
}
