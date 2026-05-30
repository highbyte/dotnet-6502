namespace Highbyte.DotNet6502.Systems.Vic20.Config;

/// <summary>
/// VIC-20 screen and memory layout constants for the emulator.
/// These follow the unexpanded VIC-20 memory map.
/// </summary>
public class Vic20Config
{
    // Standard VIC-20 text-mode dimensions (default column/row count)
    public const int Cols = 22;
    public const int Rows = 23;

    // Horizontal pixel stretching to approximate the VIC-I's non-square pixel aspect ratio.
    // On a real TV, VIC-I pixels are physically wider than tall (the VIC-20 displays only
    // 22 columns in a horizontal scan that the C64 fills with 40), so square-pixel rendering
    // would make characters appear too narrow. 2× horizontal stretching gives a close visual
    // match to real hardware.
    public const int PixelScaleX = 2;

    // Drawable area in buffer pixels (includes horizontal stretching).
    public const int DrawableAreaWidth = Cols * 8 * PixelScaleX;   // 352
    public const int DrawableAreaHeight = Rows * 8;                // 184

    // TV broadcast standard. Defines visible raster area shared with the C64 (a TV is a TV,
    // regardless of which computer is plugged in). VIC-20's smaller character area naturally
    // results in more border than the C64 within the same TV space.
    public TvModel TvModel { get; set; } = TvModel.Ntsc;

    public int MaxVisibleWidth => TvModel.MaxVisibleWidth;
    public int MaxVisibleHeight => TvModel.MaxVisibleHeight;

    // Cell-based border dimensions for the lightweight command-stream renderer
    // (which renders character cells, not raw pixels).
    public const int BorderCols = 5;
    public const int BorderRows = 3;

    // VIC-20 screen RAM: VIC-I $9005 bits[7:4]=$C encodes screen at $1000 (start of 4KB user RAM)
    public ushort ScreenStartAddress { get; set; } = 0x1000;

    // VIC-20 color RAM: always at this address regardless of RAM expansion
    public ushort ColorStartAddress { get; set; } = 0x9400;

    // VIC-I register $900F packs both:
    //   bits 7-4: background color (16 colors)
    //   bit  3  : reverse video (1=normal, 0=reverse)
    //   bits 2-0: border color   (8 colors)
    // $900E is NOT border — it is sound volume (bits 3-0) + auxiliary color (bits 7-4).
    public ushort BackgroundColorAddress { get; set; } = 0x900F;
    public ushort BorderColorAddress { get; set; } = 0x900F;

    // VIC-20 boots to a white screen with cyan border and blue text.
    public byte DefaultFgColor { get; set; } = 0x06;  // Blue (3-bit)
    public byte DefaultBgColor { get; set; } = 0x01;  // White (4-bit)
    public byte DefaultBorderColor { get; set; } = 0x03; // Cyan (3-bit)

    // CPU cycles per frame: VIC-20 NTSC runs at ~14318 cycles/frame at 60 Hz
    public ulong CpuCyclesPerFrame { get; set; } = 14318;

    public float ScreenRefreshFrequencyHz => TvModel.RefreshFrequencyHz;
}
