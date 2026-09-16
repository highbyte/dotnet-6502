using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

/// <summary>
/// The VIC-II's sprite DMA and display, by the rules of section 3.8.1 of the VIC-II article: the
/// compare in cycles 55 and 56 of the line a sprite's Y names switches its DMA on, the display
/// follows in cycle 58, the data counter base MCBASE takes the data counter MC (three on after
/// the line's fetch) in cycle 16 of every line where the expansion flip-flop is set, and the DMA
/// ends in cycle 16 once MCBASE has reached 63.
/// Cycle numbers are 1-based as in the article; the raster is positioned with 0-based offsets.
/// </summary>
public class Vic2SpriteDmaTests
{
    private const int CyclesPerLine = 63;

    private static C64 Build()
        => C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);

    /// <summary>Position the raster at the given 1-based cycle of the line (the cycle in progress).</summary>
    private static void PositionAt(C64 c64, int line, int cycle)
        => c64.Vic2.AdvanceRaster((ulong)(line * CyclesPerLine + cycle - 1));

    private static void AdvanceTo(C64 c64, int line, int cycle)
    {
        var target = (ulong)(line * CyclesPerLine + cycle - 1);
        c64.Vic2.AdvanceRaster(target - c64.Vic2.CyclesConsumedCurrentVblank);
    }

    [Fact]
    public void Dma_switches_on_at_the_compare_in_cycle_55_and_the_display_in_cycle_58()
    {
        var c64 = Build();
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, 100);
        PositionAt(c64, 100, 54);
        Assert.Equal(0, c64.Vic2.SpriteDmaMask);
        Assert.Equal(0, c64.Vic2.SpriteDisplayMask);
        AdvanceTo(c64, 100, 55);
        Assert.Equal(1, c64.Vic2.SpriteDmaMask);
        Assert.Equal(0, c64.Vic2.SpriteDisplayMask);
        AdvanceTo(c64, 100, 57);
        Assert.Equal(0, c64.Vic2.SpriteDisplayMask);
        AdvanceTo(c64, 100, 58);
        Assert.Equal(1, c64.Vic2.SpriteDisplayMask);
    }

    [Fact]
    public void An_unexpanded_sprite_fetches_a_new_row_every_line_and_ends_after_21()
    {
        var c64 = Build();
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, 100);
        PositionAt(c64, 100, 1);
        // The data counter loaded in cycle 58 of line 100 + k names row k for line 101 + k.
        for (var k = 0; k < 21; k++)
        {
            AdvanceTo(c64, 101 + k, 1);
            Assert.Equal(3 * k, c64.Vic2.SpriteMc(0));
            Assert.Equal(1, c64.Vic2.SpriteDmaMask);
        }
        AdvanceTo(c64, 121, 15);
        Assert.Equal(1, c64.Vic2.SpriteDmaMask);   // row 20 was fetched at the line's start
        AdvanceTo(c64, 121, 16);                    // the check in the first phase of cycle 16
        Assert.Equal(0, c64.Vic2.SpriteDmaMask);
        Assert.Equal(1, c64.Vic2.SpriteDisplayMask);   // still displayed on this line
        AdvanceTo(c64, 121, 58);                    // the display decision finds the DMA off
        Assert.Equal(0, c64.Vic2.SpriteDisplayMask);
    }

    [Fact]
    public void A_y_expanded_sprite_fetches_each_row_on_two_lines_and_ends_after_42()
    {
        var c64 = Build();
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, 100);
        PositionAt(c64, 100, 1);
        for (var k = 0; k < 42; k++)
        {
            AdvanceTo(c64, 101 + k, 1);
            Assert.Equal(3 * (k / 2), c64.Vic2.SpriteMc(0));
            Assert.Equal(1, c64.Vic2.SpriteDmaMask);
        }
        AdvanceTo(c64, 142, 17);
        Assert.Equal(0, c64.Vic2.SpriteDmaMask);
    }

    [Theory]
    [InlineData(54)]   // before the flip-flop check in cycle 55
    [InlineData(57)]   // after it, before MC is loaded in cycle 58
    public void Clearing_the_y_expand_bit_on_the_sprites_third_line_gives_23_lines(int writeCycle)
    {
        // VICE's spritedma/d017 tests: a Y-expanded sprite at Y 49 whose expand bit is cleared on
        // line 52 shows rows 0 and 1 twice and the rest once, 23 lines, whichever side of the
        // cycle 55 check the write lands on. Cleared, the flip-flop is set at once (rule 1), so
        // MCBASE advances on every line from line 53 on.
        var c64 = Build();
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x10);
        c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0xFF);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y + 8, 49);
        PositionAt(c64, 49, 1);
        var expectedRows = new[] { 0, 0, 1, 1 }.Concat(Enumerable.Range(2, 19)).ToArray();
        for (var i = 0; i < expectedRows.Length; i++)
        {
            AdvanceTo(c64, 50 + i, 1);
            Assert.Equal(1 << 4, c64.Vic2.SpriteDisplayMask);
            Assert.Equal(3 * expectedRows[i], c64.Vic2.SpriteMc(4));
            if (50 + i == 52)
            {
                AdvanceTo(c64, 52, writeCycle);
                c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x00);
            }
        }
        AdvanceTo(c64, 72, 17);
        Assert.Equal(0, c64.Vic2.SpriteDmaMask);
        Assert.Equal(1 << 4, c64.Vic2.SpriteDisplayMask);   // displayed until the decision in cycle 58
        AdvanceTo(c64, 72, 58);
        Assert.Equal(0, c64.Vic2.SpriteDisplayMask);
    }

    [Fact]
    public void Clearing_and_setting_the_y_expand_bit_between_cycles_17_and_54_holds_the_row()
    {
        // The sprite stretcher: clearing the bit sets the flip-flop at once, setting it again lets
        // the inversion in cycle 55 clear it, so MCBASE stands still in cycles 15 and 16 of the
        // next line and that line shows the same row. Done on every line, the row is held for as
        // long as the program keeps it up; leaving the bit cleared releases it.
        var c64 = Build();
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x00);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, 100);
        PositionAt(c64, 100, 1);
        AdvanceTo(c64, 103, 1);
        Assert.Equal(6, c64.Vic2.SpriteMc(0));                 // row 2 on line 103, unexpanded so far
        // A line's row is decided in cycles 15 and 16 of the line before, from the flip-flop as
        // cycle 55 of the line before that left it: the writes on line 103 hold from line 105.
        for (var line = 103; line < 110; line++)
        {
            AdvanceTo(c64, line, 20);
            c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x00);
            c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x01);
            AdvanceTo(c64, line + 1, 1);
            Assert.Equal(9, c64.Vic2.SpriteMc(0));             // row 3 on line 104, then held
        }
        AdvanceTo(c64, 110, 20);
        c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x00);         // released: the flip-flop stays set
        AdvanceTo(c64, 111, 1);
        Assert.Equal(9, c64.Vic2.SpriteMc(0));                 // line 111 was decided by line 109's writes
        AdvanceTo(c64, 112, 1);
        Assert.Equal(12, c64.Vic2.SpriteMc(0));                // row 4
    }

    [Theory]
    [InlineData(14, 6)]   // before the crunch cycle: the flip-flop is set by the write, cycle 16 takes MC (3 + 3)
    [InlineData(15, 7)]   // the crunch cycle: MC becomes the merge of MCBASE 3 and MC 6, and cycle 16 takes it
    [InlineData(16, 3)]   // after cycle 16's update, made with the flip-flop clear: MCBASE stays
    public void Clearing_the_y_expand_bit_with_the_flip_flop_clear_crunches_the_counter_in_cycle_15(int writeCycle, int expectedMc)
    {
        // A Y-expanded sprite from line 100: the flip-flop is clear on lines 101 and 103 (cleared
        // at the compare, inverted in cycle 55 of every line). Row 0 is fetched for 101 and 102,
        // MCBASE becomes 3 in cycle 16 of line 102, and the fetch for line 103 leaves MC at 6.
        var c64 = Build();
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, 100);
        PositionAt(c64, 103, 1);
        Assert.Equal(3, c64.Vic2.SpriteMc(0));
        AdvanceTo(c64, 103, writeCycle);
        c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x00);
        AdvanceTo(c64, 104, 1);
        Assert.Equal(expectedMc, c64.Vic2.SpriteMc(0));
    }

    [Fact]
    public void A_crunched_sprite_keeps_fetching_past_its_last_row()
    {
        // Crunched to 7 on line 103 and unexpanded from then on, MCBASE steps 7, 10, ... 61, 0, 3,
        // ... and only meets 63 twenty lines after the wrap: the DMA runs on to line 143 instead
        // of ending on line 122.
        var c64 = Build();
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, 100);
        PositionAt(c64, 103, 15);
        c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x00);
        AdvanceTo(c64, 130, 1);
        Assert.Equal(1, c64.Vic2.SpriteDmaMask);
        AdvanceTo(c64, 143, 1);
        Assert.Equal(1, c64.Vic2.SpriteDmaMask);
        AdvanceTo(c64, 144, 1);
        Assert.Equal(0, c64.Vic2.SpriteDmaMask);
    }

    [Fact]
    public void A_sprite_is_shown_again_when_its_y_is_rewritten_to_a_later_line()
    {
        var c64 = Build();
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, 100);
        PositionAt(c64, 100, 1);
        AdvanceTo(c64, 121, 17);
        Assert.Equal(0, c64.Vic2.SpriteDmaMask);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, 130);
        AdvanceTo(c64, 130, 58);
        Assert.Equal(1, c64.Vic2.SpriteDmaMask);
        Assert.Equal(1, c64.Vic2.SpriteDisplayMask);
        AdvanceTo(c64, 131, 1);
        Assert.Equal(0, c64.Vic2.SpriteMc(0));
    }

}
