namespace Highbyte.DotNet6502.Systems.Commodore64.Models;

public class Vic2ModelNTSC : Vic2ModelBase
{
    public override string Name => "NTSC";
    public override ulong Lines => 262;               // Total lines, incl. normal draw area (200 lines), border, and VBlank.
    public override ulong PixelsPerLine => 512;       // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.

    public override ulong CyclesPerLine => 64;
    public override ulong CyclesPerFrame => 64 * 262;   //CyclesPerLine * Lines;

    // TODO: Are the lines visible same ratio as PAL?  Below assumes the border stays same ratio, and VBlank is changed
    public override ulong LinesVisible => (ulong)((float)Lines * ((float)284 / 312));
    // TODO: Is the extra cycle per line compared to PAL spent in the border or in the HBlank area. Below assumes the border stays same, and HBlank is increased.
    public override ulong PixelsPerLineVisible => 403;

    // Should be?
    public override ulong HBlankPixels => PixelsPerLine - PixelsPerLineVisible;
    // Should be?
    public override ulong VBlankLines => Lines - LinesVisible;
}

public class Vic2ModelNTSC_old : Vic2ModelBase
{

    public override string Name => "NTSC_old";
    public override ulong Lines => 263;               // Total lines, incl. normal draw area (200 lines), border, and VBlank.
    public override ulong PixelsPerLine => 520;       // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.

    public override ulong CyclesPerLine => 65;
    public override ulong CyclesPerFrame => 65 * 263;    // CyclesPerLine * Lines;

    // TODO: Are the lines visible same ratio as PAL?  Below assumes the border stays same ratio, and VBlank is changed
    public override ulong LinesVisible => (ulong)((float)Lines * ((float)284 / 312));
    // TODO: Is the extra cycle per line compared to PAL spent in the border or in the HBlank area. Below assumes the border stays same, and HBlank is increased.
    public override ulong PixelsPerLineVisible => 403;

    // Should be?
    public override ulong HBlankPixels => PixelsPerLine - PixelsPerLineVisible;
    // Should be?
    public override ulong VBlankLines => Lines - LinesVisible;
}

/// <summary>
/// VIC2 PAL screen ref: https://dustlayer.com/vic-ii/2013/4/25/vic-ii-for-beginners-beyond-the-screen-rasters-cycle
/// </summary>
public class Vic2ModelPAL : Vic2ModelBase
{
    public override string Name => "PAL";
    public override ulong Lines => 312;           // Total lines, incl. normal draw area (200 lines), border, and VBlank.
    public override ulong PixelsPerLine => 504;   // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.
    public override ulong CyclesPerLine => 63;    // Total cycles per line, incl normal draw area (320 pixels), border, and HBlank

    // CyclesPerFrame => CyclesPerLine * Lines
    public override ulong CyclesPerFrame => 63 * 312;   // CyclesPerLine * Lines;

    // LineVisible => Lines - VBlankLines
    // TODO: Why is it 284? Shouldn't it be 312 - 30 = 282?
    public override ulong LinesVisible => 284;

    public override ulong PixelsPerLineVisible => 403;


    // Should be 504 - 403 = 101
    public override ulong HBlankPixels => PixelsPerLine - PixelsPerLineVisible;
    // Should be 312 - 284 = 28
    public override ulong VBlankLines => Lines - LinesVisible;
}

public abstract class Vic2ModelBase
{
    public abstract string Name { get; }

    public abstract ulong CyclesPerFrame { get; }          // CyclesPerLine * Lines;
    public abstract ulong CyclesPerLine { get; }
    public abstract ulong Lines { get; }

    public abstract ulong PixelsPerLineVisible { get; }    // PixelsPerLine - HBlankPixels;
    public abstract ulong PixelsPerLine { get; }           // CyclesPerLine * PixelsPerCPUCycle;
    public abstract ulong LinesVisible { get; }             // Lines - VBlankLines;

    public ulong PixelsPerCPUCycle => 8;
    public abstract ulong HBlankPixels { get; }
    public abstract ulong VBlankLines { get; }
}
