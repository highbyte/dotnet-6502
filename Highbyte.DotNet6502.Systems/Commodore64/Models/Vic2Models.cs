namespace Highbyte.DotNet6502.Systems.Commodore64.Models;

/// <summary>
/// NTSC old version (VIC 6567R56A)
/// </summary>
public class Vic2ModelNTSC_old : Vic2ModelBase
{
    public override string Name => "NTSC_old";
    public override ulong Lines => 262;               // Total lines, incl. normal draw area (200 lines), border, and VBlank.
    public override ulong PixelsPerLine => 512;       // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.

    public override ulong CyclesPerLine => 64;
    public override ulong CyclesPerFrame => 64 * 262;   //CyclesPerLine * Lines;

    public override ulong LinesVisible => 234;
    public override ulong PixelsPerLineVisible => 411;

    public override ulong HBlankPixels => PixelsPerLine - PixelsPerLineVisible;
    public override ulong VBlankLines => Lines - LinesVisible;

    public override ushort ConvertRasterLineToScreenLine(ushort rasterLine)
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
    public override ulong Lines => 263;               // Total lines, incl. normal draw area (200 lines), border, and VBlank.
    public override ulong PixelsPerLine => 520;       // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.

    public override ulong CyclesPerLine => 65;
    public override ulong CyclesPerFrame => 65 * 263;    // CyclesPerLine * Lines;

    public override ulong LinesVisible => 235;
    public override ulong PixelsPerLineVisible => 418;

    public override ulong HBlankPixels => PixelsPerLine - PixelsPerLineVisible;
    public override ulong VBlankLines => Lines - LinesVisible;

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

    public override ushort ConvertRasterLineToScreenLine(ushort rasterLine)
    {
        // TODO: Is there difference in conversion between RSEL 0 (24 rows) and RSEL 1 (25 rows) mode ?

        const ushort rasterLineForTopmostScreenLine = 20;
        if (rasterLine < rasterLineForTopmostScreenLine)
            //return (ushort)(rasterLine + 243);
            return (ushort)(rasterLine + (Lines - rasterLineForTopmostScreenLine));
        else
            //return (ushort)(rasterLine - 20);
            return (ushort)(rasterLine - rasterLineForTopmostScreenLine);
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
    public override ulong Lines => 312;           // Total lines, incl. normal draw area (200 lines), border, and VBlank.
    public override ulong PixelsPerLine => 504;   // Total pixels per line, incl. normal draw area (320 pixels), border, and HBlank.
    public override ulong CyclesPerLine => 63;    // Total cycles per line, incl normal draw area (320 pixels), border, and HBlank

    public override ulong CyclesPerFrame => 63 * 312;   // CyclesPerLine * Lines;

    public override ulong LinesVisible => 284;      // Max visible, includes normal screen and top/bottom border

    public override ulong PixelsPerLineVisible => 403;

    public override ulong HBlankPixels => PixelsPerLine - PixelsPerLineVisible;
    // Should be 312 - 284 = 28  (or "around" 30 as stated in some docs)
    public override ulong VBlankLines => Lines - LinesVisible;

    public override ushort ConvertRasterLineToScreenLine(ushort rasterLine)
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

    public abstract ulong CyclesPerFrame { get; }          // CyclesPerLine * Lines;
    public abstract ulong CyclesPerLine { get; }
    public abstract ulong Lines { get; }

    public abstract ulong PixelsPerLineVisible { get; }    // PixelsPerLine - HBlankPixels;
    public abstract ulong PixelsPerLine { get; }           // CyclesPerLine * PixelsPerCPUCycle;
    public abstract ulong LinesVisible { get; }             // Lines - VBlankLines;

    public ulong PixelsPerCPUCycle => 8;
    public abstract ulong HBlankPixels { get; }
    public abstract ulong VBlankLines { get; }

    public abstract ushort ConvertRasterLineToScreenLine(ushort rasterLine);
}
