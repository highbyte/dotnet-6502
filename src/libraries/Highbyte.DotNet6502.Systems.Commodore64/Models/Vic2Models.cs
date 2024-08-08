namespace Highbyte.DotNet6502.Systems.Commodore64.Models;

/// <summary>
/// NTSC old version (VIC 6567R56A)
/// </summary>
public class Vic2ModelNTSC_old : Vic2ModelBase
{
    public override string Name => "NTSC_old";
    public override ulong CyclesPerLine => 64;
    public override ulong CyclesPerFrame => 64 * 262;   //CyclesPerLine * TotalHeight;

    public override int TotalWidth => 512;       // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.
    public override int TotalHeight => 262;               // Total lines, incl. normal draw area (200 lines), border, and VBlank.

    public override int MaxVisibleWidth => 411;
    public override int MaxVisibleHeight => 234;

    public override int FirstRasterLineOfMainScreen => 51; // TODO: Verify

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
    public override ulong CyclesPerLine => 65;
    public override ulong CyclesPerFrame => 65 * 263;    // CyclesPerLine * TotalHeight;

    public override int TotalWidth => 520;       // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.
    public override int TotalHeight => 263;               // Total lines, incl. normal draw area (200 lines), border, and VBlank.


    public override int MaxVisibleWidth => 418;
    public override int MaxVisibleHeight => 235;

    public override int FirstRasterLineOfMainScreen => 51;

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
    public override ulong CyclesPerLine => 63;          // Total cycles per line, incl normal draw area (320 pixels), border, and HBlank

    public override ulong CyclesPerFrame => 63 * 312;   // CyclesPerLine * TotalHeight;

    public override int TotalWidth => 504;            // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.
    public override int TotalHeight => 312;           // Total lines, incl. normal draw area (200 lines), border, and VBlank.

    public override int MaxVisibleWidth => 403;          // Max visible, includes normal screen and left/right border
    public override int MaxVisibleHeight => 284;         // Max visible, includes normal screen and top/bottom border

    public override int FirstRasterLineOfMainScreen => 51;

    public override int HBlankWidth => TotalWidth - MaxVisibleWidth;
    // Should be 312 - 284 = 28  (or "around" 30 as stated in some docs)
    public override int VBlankHeight => TotalHeight - MaxVisibleHeight;

    public override int ConvertRasterLineToScreenLine(int rasterLine)
    {
        return rasterLine;
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

    public abstract int MaxVisibleWidth { get; }         // TotalWidth - HBlankWidth;
    public abstract int MaxVisibleHeight { get; }        // TotalHeight - VBlankHeight;

    public abstract int FirstRasterLineOfMainScreen { get; }    // The raster line where the main screen with background starts. Note that raster line 0 in NTSC variant is within the bottom border.

    public abstract int HBlankWidth { get; }
    public abstract int VBlankHeight { get; }

    public abstract int ConvertRasterLineToScreenLine(int rasterLine);

    public bool IsRasterLineInMainScreen(int rasterLine)
    {
        return rasterLine >= FirstRasterLineOfMainScreen
            && rasterLine < FirstRasterLineOfMainScreen + DrawableAreaHeight;
    }
}
