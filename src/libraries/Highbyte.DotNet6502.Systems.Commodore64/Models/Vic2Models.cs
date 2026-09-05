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
    /// Pixels between the cycle boundary after a colour register write and the pixel that first
    /// shows the new colour: the rasterizer applies a write reported in frame cycle c from pixel
    /// 8 * (c + 1) of the raster line plus this value.
    ///
    /// <para><b>How it was measured.</b> Against VICE (x64sc, CRT emulation off) with the screen
    /// column sample, after that sample had been made to start on the same cycle on every run (its
    /// earlier first-arrival calibration made the picture depend on the start cycle, and the first
    /// per-model values of -11 and +4 had compared screenshots taken at different phases). With
    /// -11 every colour edge lands within a pixel of VICE's on the 6569 and the 8565 (PAL) and on
    /// the 6567R8 (NTSC). One value for every chip, as a pipeline delay has to be. The 6567R56A is
    /// not modelled and was not measured.</para>
    ///
    /// <para><b>What the value means.</b> Split it into whole cycles and pixels:
    /// 8 * (c + 1) - 11 = 8 * (c - 1) + 5, that is five pixels into the cycle <i>before</i> the one
    /// the write is reported in. Two things are pinned: the reported cycle is the store's own bus
    /// write cycle in this emulator's cycle count (C64DeviceAccessTimingTests), and the display
    /// window anchor (<see cref="DisplayWindowStartX"/>, 124 pixels, X 24 in the second half of the
    /// chip's cycle 16) agrees with VICE's cycle tables, so pixels are placed against the chip's
    /// cycles as VICE places them. A change cannot precede its write, so the only consistent
    /// reading is that this emulator's cycle index runs about two cycles ahead of the chip's cycle
    /// as the pixel side sees it, and the chip's own register-to-pixel pipeline is the remaining
    /// five pixels. Five is plausible: the CPU's write lands in the second half of its cycle and the
    /// VIC-II's output stage delays register changes by a few pixels; hardware measurements of the
    /// border colour changing part way through a character cell are in that range.</para>
    ///
    /// <para><b>Where the two cycles could come from</b>, none of it established:</para>
    /// <list type="bullet">
    /// <item><description>The raster line's cycle origin. This emulator's line cycle 0 is where
    /// <c>CyclesConsumedCurrentVblank</c> is a multiple of the line length, and the raster line
    /// register changes there. Bauer's timing diagrams number the chip's cycles from 1 and put the
    /// X coordinate $194 (PAL) at the start of cycle 1; if the RASTER register on the chip
    /// increments at a different cycle than the one the pixel anchor was derived from, every CPU
    /// event is dated against a line origin that is shifted from the pixel origin by that
    /// difference. Check: VICE's vicii-cycle.c, where raster_line is incremented against the cycle
    /// at which its xpos table restarts.</description></item>
    /// <item><description>The CPU's access phase. The 6510 reads and writes in the second half of a
    /// cycle (phi2); the VIC-II fetches in the first half. If the cycle engine dates an access to
    /// the cycle in which the instruction's bus cycle begins while the pixel side counts from
    /// where the VIC-II's cycle begins, there is a half-cycle, four pixels, in the difference, which
    /// with rounding to a cycle boundary can look like a whole cycle.</description></item>
    /// <item><description>Interrupt and stall bookkeeping. A raster interrupt is dated to the cycle
    /// the line began and a bad line release to cycle 55; if either is placed a cycle off, every
    /// program timed from them is off by that cycle, and the two column samples are timed from
    /// exactly those. The border column sample, which never meets a bad line, gave the same value,
    /// which argues against the release being the culprit but not against the line origin.
    /// </description></item>
    /// </list>
    ///
    /// <para><b>How to pin it down.</b> Use an event whose bus cycle is fixed by the chip rather than
    /// by the CPU's count: a store issued immediately after a bad line release lands on chip cycle
    /// 55 + 3 by construction (the release cycle, then the store's three cycles before its write),
    /// whatever this emulator's cycle index says. Where VICE shows that edge, measured from the
    /// display window's left edge, gives the chip's pipeline alone; the part of the eleven pixels
    /// it does not explain is this emulator's alignment, and then belongs in the timing model (the
    /// line origin or the access phase), not here. A second, independent check: the cycle at which
    /// a program first sees the raster line register change, compared with VICE's, gives the line
    /// origin directly. Once the alignment is fixed this constant becomes the pipeline alone, about
    /// +5, and must be re-measured; until then it absorbs both, and every cycle-derived effect
    /// (colour edges, border opening, sprite positions against colour changes) is offset by the
    /// same amount, which is why nothing visible depends on the split.</para>
    /// </summary>
    public virtual int ColorChangePixelDelay => -11;

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
