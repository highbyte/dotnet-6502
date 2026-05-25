namespace Highbyte.DotNet6502.Systems.Vic20.Config;

/// <summary>
/// VIC-20 screen and memory layout constants for the emulator.
/// These follow the unexpanded VIC-20 memory map.
/// </summary>
public class Vic20Config
{
    // Standard VIC-20 text-mode dimensions
    public const int Cols = 22;
    public const int Rows = 23;
    // NTSC VIC-20 border: ~5 char columns each side, ~1 row top/bottom.
    // This gives a 256×200 visible pixel area (wider than tall), matching real hardware.
    public const int BorderCols = 5;
    public const int BorderRows = 1;

    // VIC-20 screen RAM: default location in unexpanded machine
    public ushort ScreenStartAddress { get; set; } = 0x1E00;

    // VIC-20 color RAM: always at this address regardless of RAM expansion
    public ushort ColorStartAddress { get; set; } = 0x9600;

    // Simplified color control: two separate addresses for bg and border
    public ushort BackgroundColorAddress { get; set; } = 0x900F;
    public ushort BorderColorAddress { get; set; } = 0x900E;

    // VIC-20 boots to a blue screen with white text
    public byte DefaultFgColor { get; set; } = 0x01;  // White
    public byte DefaultBgColor { get; set; } = 0x06;  // Blue
    public byte DefaultBorderColor { get; set; } = 0x03; // Cyan

    // CPU cycles per frame: VIC-20 NTSC runs at ~14318 cycles/frame at 60 Hz
    public ulong CpuCyclesPerFrame { get; set; } = 14318;

    public float ScreenRefreshFrequencyHz { get; set; } = 60.0f;
}
