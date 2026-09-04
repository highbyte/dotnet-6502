namespace Highbyte.DotNet6502.Systems.Commodore64.Models;

/// <summary>
/// NTSC old version (VIC 6567R56A)
/// </summary>
public class Vic2ModelNTSC_old : Vic2ModelBase
{
    public override string Name => "NTSC_old";
    public override TvModel TvModel => TvModel.Ntsc;
    public override ulong CyclesPerLine => 64;
    public override ulong CyclesPerFrame => 64 * 262;   //CyclesPerLine * TotalHeight;

    public override int TotalWidth => 512;       // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.
    public override int TotalHeight => 262;               // Total lines, incl. normal draw area (200 lines), border, and VBlank.

    // Chip-specific overrides — the 6567R56A has slightly different timing than the later 6567R8.
    public override int MaxVisibleWidth => 411;
    public override int MaxVisibleHeight => 234;

    public override int FirstRasterLineOfMainScreen => 51; // TODO: Verify

    // DisplayWindowStartX is left at the base class default: the 6567R56A's X coordinate for the
    // start of its first cycle has not been established here, and this variant is unfinished anyway
    // (ConvertRasterLineToScreenLine throws).

    public override int HBlankWidth => TotalWidth - MaxVisibleWidth;
    public override int VBlankHeight => TotalHeight - MaxVisibleHeight;

    public override int ConvertRasterLineToScreenLine(int rasterLine)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// NTSC new version (6567R8)
/// </summary>
public class Vic2ModelNTSC : Vic2ModelBase
{

    public override string Name => "NTSC";
    public override TvModel TvModel => TvModel.Ntsc;
    public override ulong CyclesPerLine => 65;
    public override ulong CyclesPerFrame => 65 * 263;    // CyclesPerLine * TotalHeight;

    public override int TotalWidth => 520;       // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.
    public override int TotalHeight => 263;               // Total lines, incl. normal draw area (200 lines), border, and VBlank.

    // MaxVisibleWidth (418) and MaxVisibleHeight (235) inherited from TvModel.Ntsc.

    public override int FirstRasterLineOfMainScreen => 51;

    // The 6567R8's first cycle starts at X coordinate $19c (412), and its X counter wraps to 0 after
    // 511, not after the line's 520 pixels: the 8 extra pixels of line come from one X value being
    // held later in the line, past the display window. So X 0 is 100 pixels after the start of the
    // first cycle, as on PAL, and the display window's first pixel is at 124, four pixels into the
    // line's 16th cycle. (Taking the line length as the wrap point instead gives 132, which is
    // wrong by a cycle.)
    public override int DisplayWindowStartX => 124;

    // Calibrated against VICE with the anchor above; see the base class. Differs from PAL's -11 by
    // 15 pixels, close to the two cycles by which the two chips' line lengths differ, which is a
    // hint about where the remaining model dependent difference lives.
    public override int ColorChangePixelDelay => 4;

    public override int HBlankWidth => TotalWidth - MaxVisibleWidth;
    public override int VBlankHeight => TotalHeight - MaxVisibleHeight;

    // NTSC (new) RSEL 1 (25 text lines/200 pixels = default) raster lines
    //
    // Raster line | Scr line    | Comment 
    // ------------+-------------+--------------------------
    //     0       |    243      | Raster line 0 is within the bottom border...
    //    11       |    254      | Last NORMALLY VISIBLE line of bottom border.
    //    11       |    254      | Also last line of bottom border in Vice 64 border mode full.
    //    19       |    262      | Last real (?) line of bottom border (VICE 64 Debug border mode)
    //    20       |      0      | First real (?) line of top border (VICE 64 Debug border mode)
    //    28       |      8      | First NORMALLY VISIBLE line of top border
    //    50       |     30      | Last line of top border
    //    51       |     31      | First line of screen
    //   250       |    230      | Last line of screen
    //   251       |    231      | Fist line of bottom border
    //   262       |    242      | Last raster line before wrap around to raster line 0

    // NTSC (new) RSEL 0 (24 text lines/192 pixels) raster lines
    //
    // Raster line | Comment 
    // ------------+--------------------------
    //     0       | Raster line 0 is within the bottom border...
    //    11       | Last line of bottom border
    //    28       | First line of top border
    //    54       | Last line of top border
    //    55       | First line of screen
    //   246       | Last line of screen
    //   247       | Fist line of bottom border

    public override int ConvertRasterLineToScreenLine(int rasterLine)
    {
        // TODO: Is there difference in conversion between RSEL 0 (24 rows) and RSEL 1 (25 rows) mode ?

        const int rasterLineForTopmostScreenLine = 20;
        if (rasterLine < rasterLineForTopmostScreenLine)
            //return (ushort)(rasterLine + 243);
            return (rasterLine + (TotalHeight - rasterLineForTopmostScreenLine));
        else
            //return (ushort)(rasterLine - 20);
            return (rasterLine - rasterLineForTopmostScreenLine);
    }

    // Raster x coord where CSEL 1 (40 characters, 320 pixels) screen starts: 24
    // Raster x coord where CSEL 0 (38 characters, 304 pixels) screen starts: 31
}

/// <summary>
/// PAL (6569)
/// VIC2 PAL screen ref: https://dustlayer.com/vic-ii/2013/4/25/vic-ii-for-beginners-beyond-the-screen-rasters-cycle
/// </summary>
public class Vic2ModelPAL : Vic2ModelBase
{
    public override string Name => "PAL";
    public override TvModel TvModel => TvModel.Pal;
    public override ulong CyclesPerLine => 63;          // Total cycles per line, incl normal draw area (320 pixels), border, and HBlank

    public override ulong CyclesPerFrame => 63 * 312;   // CyclesPerLine * TotalHeight;

    public override int TotalWidth => 504;            // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.
    public override int TotalHeight => 312;           // Total lines, incl. normal draw area (200 lines), border, and VBlank.

    // MaxVisibleWidth (403) and MaxVisibleHeight (284) inherited from TvModel.Pal.

    public override int FirstRasterLineOfMainScreen => 51;

    // The 6569's first cycle starts at X coordinate $194 (404) of the line's 504, and the 40 column
    // display window is at X 24-343, so its first pixel is (24 - 404) mod 504 = 124 pixels into the
    // line: four pixels into the line's 16th cycle.
    public override int DisplayWindowStartX => 124;

    // Calibrated against VICE with the anchor above; see the base class.
    public override int ColorChangePixelDelay => -11;

    public override int HBlankWidth => TotalWidth - MaxVisibleWidth;
    // Should be 312 - 284 = 28  (or "around" 30 as stated in some docs)
    public override int VBlankHeight => TotalHeight - MaxVisibleHeight;

    public override int ConvertRasterLineToScreenLine(int rasterLine)
    {
        var screenLine = rasterLine + (GetVisibleScreenStartLine() - FirstRasterLineOfMainScreen);
        if (screenLine < 0)
            screenLine += TotalHeight;
        else if (screenLine >= TotalHeight)
            screenLine -= TotalHeight;

        return screenLine;
    }


    // PAL (new) RSEL 1 (25 text lines/200 pixels = default) raster lines
    //
    // Raster line | Comment 
    // ------------+--------------------------
    //     0       | Raster line 0 within vertical blank area (normally not visible)
    //     0       | Raster line 0 also shown in VICE 64 Debug border mode
    //     8       | First line of top border (VICE 64 Full border mode = overscan area? - PART OF IT IS INVISIBLE DUE TO VBLANK?
    //    16       | First NORMALLY VISIBLE line of top border
    //    50       | Last line of top border
    //    51       | First line of screen
    //   250       | Last line of screen
    //   251       | Fist line of bottom border
    //   287       | Last NORMALLY VISIBLE line of bottom border
    //   300       | Last line of bottom border (VICE 64 Full border mode = overscan area? - PART OF IT IS INVISIBLE DUE TO VBLANK?)
    //   311       | Last real (?) line of bottom border (VICE 64 Debug border mode)


    // PAL (new) RSEL 0 (24 text lines/192 pixels) raster lines
    //
    // Raster line | Comment 
    // ------------+--------------------------
    //     0       | Raster line 0 within vertical blank area (not visible)
    //    ??       | First line of top border
    //    ??       | Last line of top border
    //    ??       | First line of screen
    //   ???       | Last line of screen
    //   ???       | Fist line of bottom border
    //   ???       | Last line of bottom border


    // Raster x coord where CSEL 1 (40 characters, 320 pixels) screen starts: ?
    // Raster x coord where CSEL 0 (38 characters, 304 pixels) screen starts: ?

}

public abstract class Vic2ModelBase
{

    public abstract string Name { get; }

    /// <summary>
    /// TV broadcast standard for this chip variant. Provides the canonical visible raster
    /// area shared across all systems on the same TV (PAL/NTSC).
    /// </summary>
    public abstract TvModel TvModel { get; }

    public virtual int TextCols => 40;           // # characters per line in text mode
    public virtual int TextRows => 25;           // # rows in text mode
    public virtual int CharacterWidth => 8;      // # pixels width per character in text mode
    public virtual int CharacterHeight => 8;     // # pixels height per character in text mode
    public virtual int DrawableAreaWidth => 320;         // # pixels in drawable area (text mode and bitmap graphics mode)
    public virtual int DrawableAreaHeight => 200;        // # pixels in drawable area  (text mode and bitmap graphics mode)


    public abstract ulong CyclesPerFrame { get; }       // CyclesPerLine * TotalHeight;
    public abstract ulong CyclesPerLine { get; }
    public virtual int PixelsPerCPUCycle => 8;


    public abstract int TotalWidth { get; }           // CyclesPerLine * PixelsPerCPUCycle;
    public abstract int TotalHeight { get; }

    /// <summary>
    /// Pixels from the start of a raster line's first cycle to the first pixel of the 40 column
    /// display window.
    ///
    /// <para>On hardware this follows from the VIC-II's own (sprite) X coordinate system, whose
    /// origin lies in the middle of a raster line rather than at its start. The display window is
    /// at X 24-343 in 40 column mode on every chip variant, and X 0 falls 100 pixels after the
    /// start of the line's first cycle on both the 6569 and the 6567R8, so the display window
    /// starts 124 pixels into the line on both: four pixels into the line's 16th cycle. The two
    /// chips differ in where their X counter wraps (after 504 on the 6569, which is its whole line;
    /// after 512 on the 6567R8, whose 520 pixel line holds one X value for an extra 8 pixels later
    /// in the line), which is why the naive "(24 - X at cycle 1) mod line length" gives the right
    /// answer for PAL and 8 too many for NTSC.</para>
    ///
    /// <para>Everything the rasterizer derives from a cycle is placed relative to this, so a value
    /// that does not match the chip shifts every raster timed effect sideways compared to hardware,
    /// while leaving the picture itself looking the same. The default places the display window in
    /// the middle of the line, which is what this emulator did before the per variant figures were
    /// established, and which variants without a documented figure keep.</para>
    /// </summary>
    public virtual int DisplayWindowStartX => (int)Math.Floor((TotalWidth - DrawableAreaWidth) / 2.0d);

    /// <summary>
    /// Pixels between a colour register write taking effect and the pixel that first shows it.
    ///
    /// <para>On hardware the value a write leaves in a colour register reaches the pixel output
    /// through a pipeline, so the change appears a few pixels later than the cycle boundary this
    /// emulator would otherwise place it on. The delay is not in the chip documentation and is not
    /// a whole number of cycles, so it cannot be derived the way <see cref="DisplayWindowStartX"/>
    /// can; it has to be measured against hardware or an emulator that models it.</para>
    ///
    /// <para>The default of zero puts a colour change exactly on the cycle boundary, which is the
    /// uncalibrated placement, and is what a variant keeps until it has been measured. The measured
    /// variants override it with calibrations against VICE, made by eye, which should be treated as
    /// provisional until a measurement replaces them. A pipeline delay cannot depend on the TV
    /// standard, so the variants should end up with the same value; that they do not yet means
    /// some other model dependent timing difference between this emulator and VICE is still being
    /// absorbed there. A negative value says the same thing: the change shows up before the cycle
    /// boundary this emulator counts from, so part of what is measured is a whole cycle of timing
    /// rather than pixels of pipeline.</para>
    ///
    /// <para>How to settle it: the calibrations so far come from one program, the screen column
    /// sample, which takes its whole timing from the bad line bus hold, the raster line register and
    /// CIA timer A, so a whole-cycle disagreement with VICE in any of those is absorbed here. Compare
    /// something that depends on none of them: the border column sample, which never meets a bad
    /// line, or better a minimal program that polls the raster line register and stores a border
    /// colour at once, run on both variants in both emulators. If the variants then agree on one
    /// small positive value, that value is the pipeline and the whole cycle gets fixed where it
    /// belongs, in the timing model. The 15 pixels between the current PAL and NTSC values is close
    /// to the two cycles by which their line lengths differ, which points at something that scales
    /// with line length, the raster counter's timing or a per-line loop, rather than at the hold.
    /// </para>
    /// </summary>
    public virtual int ColorChangePixelDelay => 0;

    // Default to the shared TV model dimensions; chip variants with non-standard pixel timing
    // (e.g., Vic2ModelNTSC_old) can override to provide chip-specific values.
    public virtual int MaxVisibleWidth => TvModel.MaxVisibleWidth;
    public virtual int MaxVisibleHeight => TvModel.MaxVisibleHeight;

    public abstract int FirstRasterLineOfMainScreen { get; }    // The raster line where the main screen with background starts. Note that raster line 0 in NTSC variant is within the bottom border.

    public abstract int HBlankWidth { get; }
    public abstract int VBlankHeight { get; }

    public abstract int ConvertRasterLineToScreenLine(int rasterLine);

    protected int GetVisibleScreenStartLine()
    {
        var topInvisibleLines = (int)Math.Floor((TotalHeight - MaxVisibleHeight) / 2.0d);
        var visibleTopBorderHeight = (int)Math.Floor((MaxVisibleHeight - DrawableAreaHeight) / 2.0d);
        return topInvisibleLines + visibleTopBorderHeight;
    }

    public bool IsRasterLineInMainScreen(int rasterLine)
    {
        return rasterLine >= FirstRasterLineOfMainScreen
            && rasterLine < FirstRasterLineOfMainScreen + DrawableAreaHeight;
    }
}
