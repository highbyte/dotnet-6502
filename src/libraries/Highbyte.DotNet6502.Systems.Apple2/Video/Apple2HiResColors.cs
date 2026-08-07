using System.Drawing;

namespace Highbyte.DotNet6502.Systems.Apple2.Video;

/// <summary>
/// Hi-res NTSC artifact colors.
///
/// The Apple II has no color hi-res mode. It shifts out one dot per pixel at 7.16 MHz, exactly
/// twice the NTSC color subcarrier, so a repeating dot pattern is indistinguishable from a
/// chroma signal and a color monitor decodes it as a hue. Which hue depends on where the lit
/// dot falls in the color cycle, which is the parity of its column across the whole scan line —
/// not its position within the byte. Because a byte carries 7 pixels and 7 is odd, the same bit
/// position alternates parity from one byte to the next, which is why a solid violet line is
/// written as alternating $55 / $2A bytes rather than a run of one value.
///
/// Bit 7 of each byte delays that byte's dots by half a dot, moving them into the opposite half
/// of the color cycle and swapping the pair to blue/orange. Two adjacent lit dots span a full
/// cycle and read as white regardless of parity.
///
/// The six colors are the same signals lo-res produces, so they are taken from the lo-res
/// palette rather than duplicated — the two cannot drift apart.
///
/// A monitor cannot resolve the two dots inside a cycle — its chroma bandwidth is roughly a tenth
/// of the dot rate — so one lit dot tints its whole cycle and colored areas come out continuous.
/// Color resolution is therefore 140 across, not 280, which is true of the hardware as well.
///
/// This is the simplified model: bit 7 selects the color of a dot rather than actually moving it
/// half a pixel, so cycles stay aligned to fixed column pairs and the color fringing a real
/// monitor shows at black/white boundaries does not appear.
/// </summary>
public static class Apple2HiResColors
{
    // Lo-res palette entries that carry the same chroma phases the hi-res dot patterns generate.
    private const int LoResBlackIndex = 0;
    private const int LoResVioletIndex = 3;
    private const int LoResBlueIndex = 6;
    private const int LoResOrangeIndex = 9;
    private const int LoResGreenIndex = 12;
    private const int LoResWhiteIndex = 15;

    public static Color Black => Apple2LoResScreen.Palette[LoResBlackIndex];
    public static Color Violet => Apple2LoResScreen.Palette[LoResVioletIndex];
    public static Color Blue => Apple2LoResScreen.Palette[LoResBlueIndex];
    public static Color Orange => Apple2LoResScreen.Palette[LoResOrangeIndex];
    public static Color Green => Apple2LoResScreen.Palette[LoResGreenIndex];
    public static Color White => Apple2LoResScreen.Palette[LoResWhiteIndex];

    /// <summary>
    /// Color of an isolated lit dot, indexed by <c>(highBitSet ? 2 : 0) | (oddColumn ? 1 : 0)</c>.
    /// These are the four colors Applesoft exposes as HCOLOR 1/2 (green/violet) and 5/6
    /// (orange/blue).
    /// </summary>
    private static readonly Color[] s_artifactColors =
    {
        Violet,   // even column, bit 7 clear  (HCOLOR=2)
        Green,    // odd column,  bit 7 clear  (HCOLOR=1)
        Blue,     // even column, bit 7 set    (HCOLOR=6)
        Orange,   // odd column,  bit 7 set    (HCOLOR=5)
    };

    /// <summary>
    /// Color a lit dot tints its cycle, from its column across the scan line and the color-shift
    /// bit of the byte it came from. Only meaningful for a dot with no lit neighbor — one that
    /// has a neighbor covers the cycle by itself and reads as white.
    /// </summary>
    public static Color GetArtifactColor(int column, bool highBitSet)
        => s_artifactColors[(highBitSet ? 2 : 0) | (column & 1)];
}
