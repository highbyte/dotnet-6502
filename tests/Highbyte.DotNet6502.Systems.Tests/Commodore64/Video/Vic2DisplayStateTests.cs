using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

/// <summary>
/// The VIC-II's vertical state per raster line: bad lines start rows (display state, RC 0), a row's
/// eighth line ends it (idle state, VCBASE advanced by a row), the display is enabled for a frame by
/// DEN during line $30, and the vertical border flip-flop is set at the bottom compare line and
/// reset at the top one only while DEN is set.
/// </summary>
public class Vic2DisplayStateTests
{
    [Fact]
    public void Rows_start_on_bad_lines_and_the_chip_idles_after_a_rows_eighth_line()
    {
        var c64 = Build();
        c64.Mem.Write(0xD011, 0x1B);   // display on, 25 rows, YSCROLL 3
        var states = RunFrame(c64);

        Assert.True(states[50].VerticalBorder);
        Assert.False(states[50].DisplayState);

        Assert.False(states[51].VerticalBorder);           // top compare line with DEN set
        Assert.True(states[51].DisplayState);              // 51 & 7 == 3: bad line
        Assert.Equal(0, states[51].RowCounter);
        Assert.Equal(0, states[51].VideoCounterBase);
        Assert.Equal(4, states[55].RowCounter);
        Assert.Equal(7, states[58].RowCounter);

        Assert.True(states[59].DisplayState);              // next bad line: next row
        Assert.Equal(0, states[59].RowCounter);
        Assert.Equal(40, states[59].VideoCounterBase);

        Assert.Equal(7, states[250].RowCounter);           // last line of row 24
        Assert.Equal(960, states[250].VideoCounterBase);
        Assert.False(states[251].DisplayState);            // idle after the row, and
        Assert.True(states[251].VerticalBorder);           // the bottom compare line sets the border
    }

    [Fact]
    public void Display_enable_is_decided_during_line_48()
    {
        // Off through line 48, on again from 49: no bad lines all frame, the chip stays idle. The
        // border still opens at line 51, since DEN is set by then, so the screen shows idle output.
        // (DEN has to be clear before line 48 begins: set at its first cycle still counts.)
        var c64 = Build();
        var states = RunFrame(c64, line => (byte)(line is >= 40 and <= 48 ? 0x0B : 0x1B));
        Assert.False(states[51].DisplayState);
        Assert.False(states[59].DisplayState);
        Assert.False(states[100].DisplayState);
        Assert.False(states[51].VerticalBorder);
        Assert.False(c64.Vic2.IsBadLine(51));

        // On during line 48, off from 49: bad lines and rows as normal, but the border never opens.
        c64 = Build();
        states = RunFrame(c64, line => (byte)(line == 48 ? 0x1B : 0x0B));
        Assert.True(states[51].DisplayState);
        Assert.Equal(40, states[59].VideoCounterBase);
        Assert.True(states[51].VerticalBorder);
        Assert.True(states[100].VerticalBorder);
    }

    [Fact]
    public void Display_off_for_the_whole_frame_keeps_the_border_closed_and_the_chip_idle()
    {
        var c64 = Build();
        var states = RunFrame(c64, _ => 0x0B);
        Assert.All(states, state => Assert.True(state.VerticalBorder));
        Assert.All(states, state => Assert.False(state.DisplayState));
    }

    [Fact]
    public void Avoiding_bad_lines_holds_the_video_counter_so_the_next_row_starts_later()
    {
        // From line 100 to 115 YSCROLL is kept one ahead of the line's low bits, so no line is a
        // bad line: row 6 (started at 99) finishes at 106, the chip idles until YSCROLL is restored
        // at 116, and row 7 starts at the next matching line, 123. Rows below shift down 16 lines.
        var c64 = Build();
        var states = RunFrame(c64, line => line is >= 100 and < 116 ? (byte)(0x18 | ((line + 1) & 7)) : (byte)0x1B);

        Assert.True(states[99].DisplayState);
        Assert.Equal(0, states[99].RowCounter);
        Assert.Equal(240, states[99].VideoCounterBase);
        Assert.Equal(7, states[106].RowCounter);
        for (var line = 107; line < 123; line++)
            Assert.False(states[line].DisplayState);
        Assert.True(states[123].DisplayState);
        Assert.Equal(0, states[123].RowCounter);
        Assert.Equal(280, states[123].VideoCounterBase);   // row 7, not row 9
    }

    [Fact]
    public void Missing_the_bottom_compare_line_keeps_the_border_open()
    {
        // 25 row mode compares at 251, 24 row mode at 247. Switching to 24 rows just for line 251
        // (and back after) means neither line ever equals the compare value in force on it.
        var c64 = Build();
        var states = RunFrame(c64, line => (byte)(line is 250 or 251 ? 0x13 : 0x1B));

        Assert.False(states[251].VerticalBorder);
        Assert.False(states[270].VerticalBorder);
        Assert.False(states[251].DisplayState);   // idle: the opened border shows idle output
    }

    [Fact]
    public void A_yscroll_write_early_in_a_line_changes_that_lines_bad_line_decision()
    {
        // Line 59 would be a bad line with YSCROLL 3. Writing YSCROLL 4 on its 5th cycle, before the
        // chip's decision cycle, makes it an ordinary line; the row that ended on 58 leaves the chip idle.
        var c64 = Build();
        c64.Mem.Write(0xD011, 0x1B);
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(59 * cyclesPerLine + 4);
        c64.Mem.Write(0xD011, 0x1C);
        c64.Vic2.AdvanceRaster(cyclesPerLine - 4);

        Assert.False(c64.Vic2.GetLineDisplayState(59).DisplayState);
        Assert.True(c64.Vic2.GetLineDisplayState(60).DisplayState);   // 60 & 7 == 4: the new bad line
        Assert.Equal(40, c64.Vic2.GetLineDisplayState(60).VideoCounterBase);
    }

    [Theory]
    [InlineData(10, 247)]   // before the left compare: the flip-flop is set at line 247's left edge
    [InlineData(40, 248)]   // after it: the armed latch is taken over as line 248 is entered
    public void Clearing_rsel_for_a_few_cycles_in_line_247_closes_the_border(int writeCycle, int firstBorderLine)
    {
        // 24 row mode compares at 247. A write is first seen by the compare in the cycle after it,
        // and the bottom compare arms a latch the flip-flop takes over at the line's left edge and
        // at the start of the next line (VICE's border/vborder2 tests).
        var c64 = Build();
        c64.Mem.Write(0xD011, 0x1B);
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(247 * cyclesPerLine + (ulong)writeCycle);
        c64.Mem.Write(0xD011, 0x13);
        c64.Vic2.AdvanceRaster(4);
        c64.Mem.Write(0xD011, 0x1B);
        c64.Vic2.AdvanceRaster(2 * cyclesPerLine);

        Assert.False(c64.Vic2.GetLineDisplayState(firstBorderLine - 1).VerticalBorder);
        Assert.True(c64.Vic2.GetLineDisplayState(firstBorderLine).VerticalBorder);
    }

    [Fact]
    public void A_write_in_a_lines_last_cycle_is_compared_on_the_next_line()
    {
        // RSEL cleared in the last cycle of line 247 is first seen in line 248, which matches neither
        // compare value; set again in line 251's last cycle it is first seen in 252. The border
        // stays open (VICE's border/vborder-33-09).
        var c64 = Build();
        c64.Mem.Write(0xD011, 0x1B);
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(248 * cyclesPerLine - 1);
        c64.Mem.Write(0xD011, 0x13);
        c64.Vic2.AdvanceRaster(4 * cyclesPerLine);
        c64.Mem.Write(0xD011, 0x1B);
        c64.Vic2.AdvanceRaster(2 * cyclesPerLine);

        Assert.False(c64.Vic2.GetLineDisplayState(248).VerticalBorder);
        Assert.False(c64.Vic2.GetLineDisplayState(251).VerticalBorder);
        Assert.False(c64.Vic2.GetLineDisplayState(252).VerticalBorder);
        Assert.False(c64.Vic2.GetLineDisplayState(253).VerticalBorder);
    }

    private static Vic2LineDisplayState[] RunFrame(C64 c64, Func<int, byte>? d011ForLine = null)
    {
        var vic2 = c64.Vic2;
        var cyclesPerLine = vic2.Vic2Model.CyclesPerLine;
        var states = new Vic2LineDisplayState[vic2.Vic2Model.TotalHeight];
        // Run one frame to settle, then record the next.
        for (var pass = 0; pass < 2; pass++)
        {
            for (var line = 0; line < vic2.Vic2Model.TotalHeight; line++)
            {
                if (d011ForLine != null)
                    c64.Mem.Write(0xD011, d011ForLine(line));
                vic2.AdvanceRaster(cyclesPerLine);
                if (pass == 1)
                    states[line] = vic2.GetLineDisplayState(line);
            }
        }
        return states;
    }

    private static C64 Build()
    {
        return C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);
    }
}
