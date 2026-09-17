using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

/// <summary>
/// The sprite collision registers read in the middle of a raster line. A collision is latched as
/// its pixels are output, so a read reports those the beam has passed and no later ones: the
/// sprite pixels up to four before the start of the read's cycle. The read clears the register as
/// its cycle ends, and the collisions of the twelve pixels in between are lost. What the line
/// outputs after that is latched as usual and shows in the next read.
/// Cycles are 0-based offsets into the line. On PAL the line starts at X coordinate 404 of 504,
/// so a sprite at X 100 outputs its first pixel as pixel 200 of the line, the first of cycle 25.
/// </summary>
public class Vic2CollisionReadTimingTests
{
    private const int CyclesPerLine = 63;
    private const int SpriteLine = 52;        // the first line the sprites are displayed on
    private const int SpriteX = 100;          // pixel 200 of the line

    [Theory]
    [InlineData(25, 0)]   // reports pixels up to 195
    [InlineData(26, 3)]   // reports pixels up to 203
    public void A_sprite_collision_read_reports_the_pixels_the_beam_has_passed(int readCycle, int expected)
    {
        var c64 = BuildWithSprites(SpriteX, foreground: false);
        AdvanceTo(c64, SpriteLine, readCycle);
        Assert.Equal(expected, c64.Mem.Read(Vic2Addr.SPRITE_TO_SPRITE_COLLISION));
    }

    [Theory]
    [InlineData(SpriteX, 0)]        // pixel 200: between what the read at cycle 25 reports and the end of its cycle, lost
    [InlineData(SpriteX + 7, 0)]    // pixel 207: the last one lost
    [InlineData(SpriteX + 8, 3)]    // pixel 208: after the read's cycle, latched for the next read
    public void A_sprite_collision_read_clears_the_register_as_its_cycle_ends(int spriteX, int expectedInNextRead)
    {
        var c64 = BuildWithSprites(spriteX, foreground: false);
        AdvanceTo(c64, SpriteLine, 25);
        Assert.Equal(0, c64.Mem.Read(Vic2Addr.SPRITE_TO_SPRITE_COLLISION));
        AdvanceTo(c64, SpriteLine, 30);
        Assert.Equal(expectedInNextRead, c64.Mem.Read(Vic2Addr.SPRITE_TO_SPRITE_COLLISION));
        // The line's end latches nothing that a read has reported or cleared.
        AdvanceTo(c64, SpriteLine + 1, 10);
        Assert.Equal(0, c64.Mem.Read(Vic2Addr.SPRITE_TO_SPRITE_COLLISION));
    }

    [Fact]
    public void A_sprite_collision_not_read_during_the_line_is_latched_when_the_line_ends()
    {
        var c64 = BuildWithSprites(SpriteX, foreground: false);
        AdvanceTo(c64, SpriteLine + 1, 10);
        Assert.Equal(3, c64.Mem.Read(Vic2Addr.SPRITE_TO_SPRITE_COLLISION));
    }

    [Theory]
    [InlineData(25, 0)]
    [InlineData(26, 1)]
    public void A_background_collision_read_reports_the_pixels_the_beam_has_passed(int readCycle, int expected)
    {
        var c64 = BuildWithSprites(SpriteX, foreground: true);
        AdvanceTo(c64, SpriteLine, readCycle);
        Assert.Equal(expected, c64.Mem.Read(Vic2Addr.SPRITE_TO_BACKGROUND_COLLISION));
    }

    [Theory]
    [InlineData(SpriteX + 7, 0)]
    [InlineData(SpriteX + 8, 1)]
    public void A_background_collision_read_clears_the_register_as_its_cycle_ends(int spriteX, int expectedInNextRead)
    {
        var c64 = BuildWithSprites(spriteX, foreground: true);
        AdvanceTo(c64, SpriteLine, 25);
        Assert.Equal(0, c64.Mem.Read(Vic2Addr.SPRITE_TO_BACKGROUND_COLLISION));
        AdvanceTo(c64, SpriteLine, 30);
        Assert.Equal(expectedInNextRead, c64.Mem.Read(Vic2Addr.SPRITE_TO_BACKGROUND_COLLISION));
        // The line's end latches nothing that a read has reported or cleared. (The generator
        // resolves a line's remaining collisions when it draws the first cycle of the next.)
        AdvanceTo(c64, SpriteLine + 1, 10);
        ((Vic2Rasterizer)c64.RenderProvider!).CatchUpToVic2();
        Assert.Equal(0, c64.Mem.Read(Vic2Addr.SPRITE_TO_BACKGROUND_COLLISION));
    }

    /// <summary>
    /// Sprites 0 and 1 at the same position, one line high in effect: a single pixel, the
    /// leftmost, in every row, displayed from <see cref="SpriteLine"/>. With
    /// <paramref name="foreground"/> only sprite 0, over idle graphics that are foreground in
    /// every column (YSCROLL 7 makes line 55 the first bad line, so the display window's lines
    /// before it show the byte at $3FFF). The raster is left at the start of the sprite line.
    /// </summary>
    private static C64 BuildWithSprites(int spriteX, bool foreground)
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
            RenderProviderType = typeof(Vic2Rasterizer),
            Vic2RasterizerPerLineSprites = true,
        }, NullLoggerFactory.Instance);
        c64.Mem.Write(0xD011, 0x1F);
        c64.Mem.Write(0xD016, 0xC8);
        c64.Mem.Write(0xD018, 0x14);   // the screen, and with it the sprite pointers, at $0400
        c64.Vic2.Vic2Mem[0x3FFF] = (byte)(foreground ? 0xFF : 0x00);
        const int pointer = 13;   // $0340
        for (var row = 0; row < 21; row++)
            c64.Vic2.Vic2Mem[(ushort)(pointer * 64 + row * 3)] = 0x80;
        c64.Vic2.Vic2Mem[0x07F8] = pointer;
        c64.Vic2.Vic2Mem[0x07F9] = pointer;
        c64.Mem.Write(Vic2Addr.SPRITE_0_X, (byte)spriteX);
        c64.Mem.Write(Vic2Addr.SPRITE_0_X + 2, (byte)spriteX);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, SpriteLine - 1);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y + 2, SpriteLine - 1);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, (byte)(foreground ? 0x01 : 0x03));
        AdvanceTo(c64, SpriteLine, 0);
        return c64;
    }

    private static void AdvanceTo(C64 c64, int line, int cycle)
    {
        var target = (ulong)(line * CyclesPerLine + cycle);
        c64.Vic2.AdvanceRaster(target - c64.Vic2.CyclesConsumedCurrentVblank);
    }
}
