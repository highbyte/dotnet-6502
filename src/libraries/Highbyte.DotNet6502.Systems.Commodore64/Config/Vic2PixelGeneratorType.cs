namespace Highbyte.DotNet6502.Systems.Commodore64.Config;

/// <summary>
/// The pixel generator the Vic2Rasterizer render provider draws with.
/// </summary>
public enum Vic2PixelGeneratorType
{
    /// <summary>
    /// The VIC-II's graphics sequencer followed pixel by pixel: XSCROLL, the mode bits and the
    /// memory pointers take effect where the chip applies them, so mid-line changes come out as on
    /// hardware. The default.
    /// </summary>
    Sequencer,

    /// <summary>
    /// The earlier generator, drawing 8-pixel blocks with the display registers sampled once per
    /// line. Faster, and kept unchanged as a fallback for hosts where the sequencer's cost matters;
    /// it receives no new features.
    /// </summary>
    Legacy,
}
