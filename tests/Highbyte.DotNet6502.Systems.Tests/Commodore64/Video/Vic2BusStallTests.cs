using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

/// <summary>
/// The VIC-II steals the bus from the CPU: 40 cycles on every bad line (BA low from cycle 12,
/// video matrix fetches in cycles 15-54, BA high at 55) and two cycles per sprite with DMA on,
/// BA low three cycles ahead. A CPU read inside such a window waits until the window ends; writes
/// do not wait. Cycle numbers below are 1-based as in the VIC-II documentation; the raster is
/// positioned with 0-based offsets.
/// </summary>
public class Vic2BusStallTests
{
    private const ushort Start = 0x1000;
    private const int BadLine = 0x33;          // YSCROLL 3: lines $33, $3B, ... are bad lines

    private static C64 Build(byte[] program)
    {
        var c64 = C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);
        c64.Mem.StoreData(Start, program);
        c64.CPU.PC = Start;
        return c64;
    }

    private static ulong Step(C64 c64)
    {
        c64.ExecuteOneInstruction(out var result);
        return result.CyclesConsumed;
    }

    /// <summary>Position the raster so that the next bus cycle is the given 1-based cycle of the line.</summary>
    private static void PositionAt(C64 c64, int line, int cycle)
        => c64.Vic2.AdvanceRaster((ulong)line * c64.Vic2.Vic2Model.CyclesPerLine + (ulong)(cycle - 1));

    private static void EnableDisplay(C64 c64, int yScroll = 3)
        => c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, (byte)(0x18 | yScroll));

    [Theory]
    [InlineData(12, 43)]   // BA goes low on cycle 12: a read there waits until cycle 55
    [InlineData(14, 41)]
    [InlineData(15, 40)]
    [InlineData(54, 1)]
    [InlineData(55, 0)]    // BA is high again
    [InlineData(11, 43)]   // the opcode fetch is free, but NOP's second read lands on cycle 12
    [InlineData(10, 0)]
    public void Read_on_a_bad_line_waits_until_the_video_matrix_fetch_is_over(int cycle, int expectedStall)
    {
        var c64 = Build([0xEA]);   // NOP: two reads, on its first and second cycle
        EnableDisplay(c64);
        PositionAt(c64, BadLine, cycle);

        var cycles = Step(c64);

        Assert.Equal(2 + (ulong)expectedStall, cycles);
        Assert.Equal((ulong)BadLine * 63 + (ulong)(cycle - 1) + cycles, c64.Vic2.CyclesConsumedCurrentVblank);
    }

    [Fact]
    public void Writes_are_not_stalled_but_the_next_read_is()
    {
        // STA $2000 with its write on cycle 12 (reads on 9-11) runs at full speed; the NOP that
        // follows reads on cycle 13 and waits.
        var c64 = Build([0x8D, 0x00, 0x20, 0xEA]);
        EnableDisplay(c64);
        PositionAt(c64, BadLine, 9);

        Assert.Equal(4UL, Step(c64));
        Assert.Equal(2 + 42UL, Step(c64));
    }

    [Theory]
    [InlineData(0x0B, BadLine)]        // display disabled
    [InlineData(0x1B, BadLine + 1)]    // display enabled, but (line & 7) != YSCROLL
    [InlineData(0x1B, 0x2B)]           // matching YSCROLL, but above the bad-line range
    [InlineData(0x1B, 0xFB)]           // matching YSCROLL, but below the bad-line range
    public void No_bad_line_no_stall(byte control, int line)
    {
        var c64 = Build([0xEA]);
        c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, control);
        PositionAt(c64, line, 12);

        Assert.Equal(2UL, Step(c64));
    }

    [Fact]
    public void Display_switched_off_after_line_48_still_has_bad_lines_for_the_rest_of_the_frame()
    {
        // DEN is sampled during line $30 only; clearing it afterwards does not stop the fetches.
        var c64 = Build([0xEA]);
        EnableDisplay(c64);
        PositionAt(c64, 0x31, 1);
        c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, 0x0B);
        c64.Vic2.AdvanceRaster((ulong)(BadLine - 0x31) * 63 + 11);   // to the bad line, cycle 12

        Assert.Equal(2 + 43UL, Step(c64));
    }

    [Fact]
    public void Display_switched_on_after_line_48_has_no_bad_lines_until_the_next_frame()
    {
        var c64 = Build([0xEA]);
        c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, 0x0B);
        PositionAt(c64, 0x31, 1);
        EnableDisplay(c64);
        c64.Vic2.AdvanceRaster((ulong)(BadLine - 0x31) * 63 + 11);

        Assert.Equal(2UL, Step(c64));
    }

    [Fact]
    public void Bad_line_follows_a_change_of_yscroll()
    {
        var c64 = Build([0xEA]);
        EnableDisplay(c64, yScroll: 4);
        PositionAt(c64, BadLine, 12);      // $33 is not a bad line with YSCROLL 4 ...

        Assert.Equal(2UL, Step(c64));

        c64 = Build([0xEA]);
        EnableDisplay(c64, yScroll: 4);
        PositionAt(c64, BadLine + 1, 12);  // ... but $34 is

        Assert.Equal(2 + 43UL, Step(c64));
    }

    [Theory]
    [InlineData(55, 5)]   // sprite 0: pointer fetch on cycle 58, BA low from 55, high at 60
    [InlineData(58, 2)]
    [InlineData(60, 0)]
    public void Read_during_sprite_0_dma_waits_until_its_fetch_is_over(int cycle, int expectedStall)
    {
        var c64 = Build([0xEA]);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, (byte)BadLine);   // DMA on for lines $33..$47
        PositionAt(c64, BadLine + 5, cycle);

        Assert.Equal(2 + (ulong)expectedStall, Step(c64));
    }

    [Theory]
    [InlineData(56, 5)]   // 6567R8: sprite 0's pointer fetch is on cycle 59, one later than the 6569's, BA low from 56
    [InlineData(59, 2)]
    [InlineData(61, 0)]
    public void Read_during_sprite_0_dma_on_ntsc_waits_one_cycle_later_than_on_pal(int cycle, int expectedStall)
    {
        // The 6567R8's two extra cycles per line are not spread over the sprite fetches: sprites
        // 0-7 fetch at cycles 59, 61, 63, 65, 2, 4, 6, 8 (VICE's cycle tables), so the CPU gets the
        // bus back at cycle 10 of the next line with all eight active, not 11 as on the 6569.
        var c64 = C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64NTSC", Vic2Model = "NTSC" }, NullLoggerFactory.Instance);
        c64.Mem.StoreData(Start, [0xEA]);
        c64.CPU.PC = Start;
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, (byte)BadLine);
        PositionAt(c64, BadLine + 5, cycle);

        Assert.Equal(2 + (ulong)expectedStall, Step(c64));
    }

    [Fact]
    public void All_eight_sprites_release_the_bus_on_cycle_11_on_pal_and_10_on_ntsc()
    {
        foreach (var (model, vic2Model, releaseCycle) in new[] { ("C64PAL", "PAL", 11), ("C64NTSC", "NTSC", 10) })
        {
            var c64 = C64.BuildC64(new C64Config { LoadROMs = false, C64Model = model, Vic2Model = vic2Model }, NullLoggerFactory.Instance);
            c64.Mem.StoreData(Start, [0xEA]);
            c64.CPU.PC = Start;
            c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0xFF);
            for (var n = 0; n < 8; n++)
                c64.Mem.Write((ushort)(Vic2Addr.SPRITE_0_Y + 2 * n), (byte)BadLine);
            // A read on the last cycle before sprite 0's window waits through all eight fetches.
            var firstStalledCycle = model == "C64PAL" ? 55 : 56;
            PositionAt(c64, BadLine + 5, firstStalledCycle);
            var cyclesPerLine = (int)c64.Vic2.Vic2Model.CyclesPerLine;
            var expectedStall = cyclesPerLine - firstStalledCycle + releaseCycle;
            Assert.Equal(2 + (ulong)expectedStall, Step(c64));
        }
    }

    [Fact]
    public void Adjacent_sprites_keep_BA_low_across_their_fetches()
    {
        // Sprites 0 and 1: windows 55-59 and 57-61 merge into 55-61.
        var c64 = Build([0xEA]);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x03);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, (byte)BadLine);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y + 2, (byte)BadLine);
        PositionAt(c64, BadLine + 5, 55);

        Assert.Equal(2 + 7UL, Step(c64));
    }

    [Fact]
    public void Sprite_3_dma_at_the_start_of_the_next_line_pulls_BA_low_at_the_end_of_this_one()
    {
        // Sprite 3's pointer fetch is on cycle 1 of the following line; BA is low from cycle 61 of
        // this line until cycle 3 of the next.
        var c64 = Build([0xEA, 0xEA]);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x08);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y + 6, (byte)BadLine);
        PositionAt(c64, BadLine + 5, 62);

        Assert.Equal(2 + 4UL, Step(c64));                 // cycle 62 -> the read happens on cycle 3 of the next line
        Assert.Equal((ulong)(BadLine + 6) * 63 + 4, c64.Vic2.CyclesConsumedCurrentVblank);   // NOP's second read on cycle 4
    }

    [Fact]
    public void Sprite_dma_ends_after_21_lines_or_42_when_y_expanded()
    {
        var c64 = Build([0xEA]);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, (byte)BadLine);
        PositionAt(c64, BadLine + 21, 58);
        Assert.Equal(2UL, Step(c64));

        c64 = Build([0xEA]);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_Y_EXPAND, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, (byte)BadLine);
        PositionAt(c64, BadLine + 21, 58);
        Assert.Equal(2 + 2UL, Step(c64));
    }

    [Fact]
    public void Disabling_a_sprite_mid_run_does_not_stop_its_dma()
    {
        // The enable bit only gates the switch-on; a sprite already fetching keeps its DMA for the
        // rest of its rows. STA $D015 with A=0 writes on cycle 54; the NOP that follows reads on
        // cycle 55, where sprite 0's window starts as before.
        var c64 = Build([0x8D, 0x15, 0xD0, 0xEA]);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, (byte)BadLine);
        c64.CPU.A = 0;
        PositionAt(c64, BadLine + 5, 51);

        Assert.Equal(4UL, Step(c64));
        Assert.Equal(2 + 5UL, Step(c64));
    }

    [Fact]
    public void A_y_position_written_after_the_raster_passed_it_does_not_start_dma()
    {
        // Parking sprites at a line the raster has already passed (a multiplexer's habit) costs the
        // CPU nothing until the raster comes round to that line again.
        var c64 = Build([0xEA]);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0xFF);
        PositionAt(c64, 40, 1);
        for (var n = 0; n < 8; n++)
            c64.Mem.Write((ushort)(Vic2Addr.SPRITE_0_Y + n * 2), 30);   // 30 < 40: missed the compare
        c64.Vic2.AdvanceRaster(63 * 5 + 54);                             // line 45, cycle 55

        Assert.Equal(2UL, Step(c64));
        Assert.Equal(0, c64.Vic2.SpriteDmaMask);
    }

    [Fact]
    public void Dma_switches_on_when_the_raster_reaches_the_sprite_y()
    {
        var c64 = Build([0xEA]);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0x01);
        c64.Mem.Write(Vic2Addr.SPRITE_0_Y, 100);
        PositionAt(c64, 99, 1);
        Assert.Equal(0, c64.Vic2.SpriteDmaMask);

        c64.Vic2.AdvanceRaster(63);                                       // into line 100
        Assert.Equal(1, c64.Vic2.SpriteDmaMask);

        c64.Vic2.AdvanceRaster(63 * 20);                                  // line 120: last row
        Assert.Equal(1, c64.Vic2.SpriteDmaMask);
        c64.Vic2.AdvanceRaster(63);                                       // line 121: off
        Assert.Equal(0, c64.Vic2.SpriteDmaMask);
    }

    [Fact]
    public void A_raster_interrupt_on_a_line_jumped_over_by_a_long_stall_is_still_raised()
    {
        // Bad line plus eight sprites: BA is low from cycle 12 through cycle 13 of the next line, so
        // NOP's read on cycle 12 spans the whole of line $33 and lands on line $34. A raster
        // interrupt configured for line $34 must still fire, dated to that line's first cycle.
        var c64 = Build([0xEA, 0xEA]);
        EnableDisplay(c64);
        c64.Mem.Write(Vic2Addr.SPRITE_ENABLE, 0xFF);
        for (var n = 0; n < 8; n++)
            c64.Mem.Write((ushort)(Vic2Addr.SPRITE_0_Y + n * 2), (byte)BadLine);
        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, BadLine + 1);
        c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, 0x1B);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0x01);
        PositionAt(c64, BadLine, 12);

        var cycles = Step(c64);

        Assert.True(cycles > 63, $"expected a stall spanning the line, got {cycles} cycles");
        Assert.Equal(BadLine + 1, c64.Vic2.CurrentRasterLine);
        Assert.True(c64.Vic2.Vic2IRQ.IsTriggered(IRQSource.RasterCompare));
        Assert.True(c64.CPU.IRQ);
    }

    [Fact]
    public void A_frame_with_the_display_on_still_ends_on_the_frame_boundary()
    {
        // NOP loop. With DEN set the CPU loses 40-43 cycles on each of the 25 bad lines; the frame
        // loop must still hand back exactly one frame of raster time.
        var c64 = Build([0xEA, 0x4C, 0x00, 0x10]);
        EnableDisplay(c64);

        c64.ExecuteOneFrame();
        var afterFirst = c64.Vic2.CyclesConsumedCurrentVblank;
        c64.ExecuteOneFrame();

        Assert.True(afterFirst < 8, $"raster position after a frame: {afterFirst}");
        Assert.True(c64.Vic2.CyclesConsumedCurrentVblank < 8);
    }
}
