namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Represents a TV broadcast standard's visible raster area.
/// A TV is a TV regardless of which computer is plugged in — both PAL and NTSC TVs have a fixed
/// visible area determined by the broadcast standard. The character/graphics area each computer
/// generates fills a portion of this visible area; the rest is border.
///
/// The pixel counts here use the canonical reference of the VIC-II (C64) chip, which sets the
/// convention. Other systems (e.g., VIC-20) can use these same TV dimensions even though their
/// internal pixel clocks may differ slightly — for emulator purposes treating the visible
/// buffer as the same size is a clean simplification.
/// </summary>
public sealed class TvModel
{
    public string Name { get; }
    public int MaxVisibleWidth { get; }
    public int MaxVisibleHeight { get; }
    public float RefreshFrequencyHz { get; }

    public TvModel(string name, int maxVisibleWidth, int maxVisibleHeight, float refreshFrequencyHz)
    {
        Name = name;
        MaxVisibleWidth = maxVisibleWidth;
        MaxVisibleHeight = maxVisibleHeight;
        RefreshFrequencyHz = refreshFrequencyHz;
    }

    /// <summary>
    /// NTSC broadcast standard — visible raster area as observed on a typical CRT TV.
    /// Values originally from the VIC-II 6567 NTSC model.
    /// </summary>
    public static readonly TvModel Ntsc = new("NTSC", maxVisibleWidth: 418, maxVisibleHeight: 235, refreshFrequencyHz: 60.0f);

    /// <summary>
    /// PAL broadcast standard — visible raster area as observed on a typical CRT TV.
    /// Values originally from the VIC-II 6569 PAL model.
    /// </summary>
    public static readonly TvModel Pal = new("PAL", maxVisibleWidth: 403, maxVisibleHeight: 284, refreshFrequencyHz: 50.0f);
}
