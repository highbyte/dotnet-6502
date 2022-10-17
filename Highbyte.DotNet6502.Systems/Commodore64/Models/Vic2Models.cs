namespace Highbyte.DotNet6502.Systems.Commodore64.Models
{
    public class Vic2ModelNTSC : Vic2ModelBase
    {
        public override string Name => "NTSC";
        public override int Lines => 262;               // Total lines, incl. normal draw area (200 lines), border, and VBlank.
        public override int PixelsPerLine => 512;       // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.

        public override int CyclesPerLine => 64;
        public override int CyclesPerFrame => CyclesPerLine * Lines;

        // TODO: Are the lines visible same ratio as PAL?  Below assumes the border stays same ratio, and VBlank is changed
        public override int LinesVisible => (int)((float)Lines * ((float)284 / 312));
        // TODO: Is the extra cycle per line compared to PAL spent in the border or in the HBlank area. Below assumes the border stays same, and HBlank is increased.
        public override int PixelsPerLineVisible => 403;

        // Should be?
        public override int HBlankPixels => PixelsPerLine - PixelsPerLineVisible;
        // Should be?
        public override int VBlankLines => Lines - LinesVisible;
    }

    public class Vic2ModelNTSC_old : Vic2ModelBase
    {

        public override string Name => "NTSC_old";
        public override int Lines => 263;               // Total lines, incl. normal draw area (200 lines), border, and VBlank.
        public override int PixelsPerLine => 520;       // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.

        public override int CyclesPerLine => 65;
        public override int CyclesPerFrame => CyclesPerLine * Lines;

        // TODO: Are the lines visible same ratio as PAL?  Below assumes the border stays same ratio, and VBlank is changed
        public override int LinesVisible => (int)((float)Lines * ((float)284 / 312));
        // TODO: Is the extra cycle per line compared to PAL spent in the border or in the HBlank area. Below assumes the border stays same, and HBlank is increased.
        public override int PixelsPerLineVisible => 403;

        // Should be?
        public override int HBlankPixels => PixelsPerLine - PixelsPerLineVisible;
        // Should be?
        public override int VBlankLines => Lines - LinesVisible;
    }

    /// <summary>
    /// VIC2 PAL screen ref: https://dustlayer.com/vic-ii/2013/4/25/vic-ii-for-beginners-beyond-the-screen-rasters-cycle
    /// </summary>
    public class Vic2ModelPAL : Vic2ModelBase
    {
        public override string Name => "PAL";
        public override int Lines => 312;           // Total lines, incl. normal draw area (200 lines), border, and VBlank.
        public override int PixelsPerLine => 504;   // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.
        public override int CyclesPerLine => 63;    // Total cycles per line, incl normal draw area (320 pixels), border, and HBlank

        // CyclesPerFrame => CyclesPerLine * Lines
        public override int CyclesPerFrame => CyclesPerLine * Lines;

        // LineVisible => Lines - VBlankLines
        // TODO: Why is it 284? Shouldn't it be 312 - 30 = 282?
        public override int LinesVisible => 284;

        public override int PixelsPerLineVisible => 403;


        // Should be 504 - 403 = 101
        public override int HBlankPixels => PixelsPerLine - PixelsPerLineVisible;
        // Should be 312 - 284 = 28
        public override int VBlankLines => Lines - LinesVisible;
    }

    public abstract class Vic2ModelBase
    {
        public abstract string Name { get; }

        public abstract int CyclesPerFrame { get; }          // CyclesPerLine * Lines;
        public abstract int CyclesPerLine { get; }
        public abstract int Lines { get; }

        public abstract int PixelsPerLineVisible { get; }    // PixelsPerLine - HBlankPixels;
        public abstract int PixelsPerLine { get; }           // CyclesPerLine * PixelsPerCPUCycle;
        public abstract int LinesVisible { get; }             // Lines - VBlankLines;

        public int PixelsPerCPUCycle => 8;
        public abstract int HBlankPixels { get; }
        public abstract int VBlankLines { get; }
    }
}
