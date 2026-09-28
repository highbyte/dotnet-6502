using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.TimerAndPeripheral;

/// <summary>
/// A CIA timer counts down once per cycle and underflows in the cycle after it shows 1, where it
/// shows the latch again, so the counter never reads 0 while it runs; the latch shows for two
/// cycles before the next decrement, so a continuous timer with latch N has a period of N + 1
/// cycles, and a latch of 0 counts like a latch of 1. The
/// chip's pipeline sits between a control write and the counter: after a start the counter holds
/// through the two cycles after the write and shows its first decrement on the third; a force load
/// shows the latch two cycles after the write and the first decrement from it two cycles later; a
/// stop lets the counter move for two more cycles. The counter register shows the live value at any
/// point, whether the timer is counting or stopped.
/// </summary>
public class CiaTimerCountingTests
{
    private static C64 Build() =>
        C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);

    private static void Program(C64 c64, ushort latch, byte control)
    {
        c64.Mem.Write(CiaAddr.CIA1_TIMALO, (byte)(latch & 0xFF));
        c64.Mem.Write(CiaAddr.CIA1_TIMAHI, (byte)(latch >> 8));
        c64.Mem.Write(CiaAddr.CIA1_CIACRA, control);
    }

    private static byte Flags(C64 c64) => (byte)(c64.Cia1.InterruptControlLoad(0) & 0x01);

    private static int Counter(C64 c64) => c64.Cia1.TimerAHILoad(0) << 8 | c64.Cia1.TimerALOLoad(0);

    [Fact]
    public void Continuous_timer_underflows_every_latch_plus_one_cycles()
    {
        var c64 = Build();
        Program(c64, latch: 3, control: 0x01);   // continuous, start

        c64.Cia1.ProcessTimers(2);
        Assert.Equal(3, Counter(c64));           // held through the two cycles after the write
        Assert.Equal(0, Flags(c64));
        c64.Cia1.ProcessTimers(2);
        Assert.Equal(1, Counter(c64));           // then 2, 1
        Assert.Equal(0, Flags(c64));

        c64.Cia1.ProcessTimers(1);
        Assert.Equal(3, Counter(c64));           // the cycle after 1: underflow, the latch reloaded
        Assert.Equal(1, Flags(c64));             // timer A's flag shows in the underflow cycle

        c64.Cia1.ProcessTimers(1);
        Assert.Equal(3, Counter(c64));           // the latch held a second cycle
        Assert.Equal(0, Flags(c64));             // the flag was read away above

        c64.Cia1.ProcessTimers(3);
        Assert.Equal(1, Flags(c64));             // next period's 0
        c64.Cia1.ProcessTimers(3);
        Assert.Equal(0, Flags(c64));             // the period is 4, not 5: the next flag is a cycle on
        c64.Cia1.ProcessTimers(1);
        Assert.Equal(1, Flags(c64));
    }

    [Fact]
    public void One_shot_timer_stops_after_its_underflow_with_the_latch_in_the_counter()
    {
        var c64 = Build();
        Program(c64, latch: 1, control: 0x09);   // one-shot, start

        c64.Cia1.ProcessTimers(4);               // 1 held twice, then the underflow (the flag with it)
        Assert.Equal(1, Flags(c64));
        Assert.Equal(0, c64.Cia1.TimerAControlLoad(0) & 0x01);   // start bit cleared
        Assert.Equal(1, Counter(c64));           // the latch, not a wrapped count

        c64.Cia1.ProcessTimers(100);
        Assert.Equal(0, Flags(c64));             // stopped: no further underflows
        Assert.Equal(1, Counter(c64));
    }

    [Fact]
    public void Counter_reads_the_live_value_while_counting_and_moves_two_more_cycles_after_a_stop()
    {
        var c64 = Build();
        Program(c64, latch: 0x1000, control: 0x01);

        c64.Cia1.ProcessTimers(0x10);
        Assert.Equal(0x0FF2, Counter(c64));      // 16 cycles, the first two held

        c64.Cia1.TimerAControlStore(0, 0x00);    // stop
        Assert.Equal(0x0FF2, Counter(c64));
        c64.Cia1.ProcessTimers(1);
        Assert.Equal(0x0FF1, Counter(c64));      // still moving
        c64.Cia1.ProcessTimers(1);
        Assert.Equal(0x0FF0, Counter(c64));
        c64.Cia1.ProcessTimers(0x100);
        Assert.Equal(0x0FF0, Counter(c64));      // stopped

        c64.Cia1.TimerAControlStore(0, 0x01);    // resume from where it stopped
        c64.Cia1.ProcessTimers(0x10);
        Assert.Equal(0x0FE2, Counter(c64));
    }

    [Fact]
    public void Force_load_shows_the_latch_two_cycles_after_the_write_and_counts_from_it_two_cycles_later()
    {
        var c64 = Build();
        Program(c64, latch: 0x0100, control: 0x01);
        c64.Cia1.ProcessTimers(0x80);
        Assert.Equal(0x0082, Counter(c64));

        c64.Cia1.TimerAControlStore(0, 0x11);    // force load + start
        Assert.Equal(0x0082, Counter(c64));      // the running count goes on for a cycle
        c64.Cia1.ProcessTimers(1);
        Assert.Equal(0x0081, Counter(c64));
        c64.Cia1.ProcessTimers(1);
        Assert.Equal(0x0100, Counter(c64));      // the latch, two cycles after the write
        c64.Cia1.ProcessTimers(1);
        Assert.Equal(0x0100, Counter(c64));      // held
        c64.Cia1.ProcessTimers(1);
        Assert.Equal(0x00FF, Counter(c64));      // counting again

        c64.Cia1.ProcessTimers(0xFE);
        Assert.Equal(0, Flags(c64));
        c64.Cia1.ProcessTimers(1);
        Assert.Equal(1, Flags(c64));             // the underflow 0x100 cycles from the reload, the flag with it
    }

    [Fact]
    public void Force_load_on_a_stopped_timer_loads_the_latch_and_starts_it()
    {
        var c64 = Build();
        Program(c64, latch: 0x0010, control: 0x00);
        Assert.Equal(0x0010, Counter(c64));      // the high byte's write loaded the counter

        c64.Cia1.TimerAControlStore(0, 0x11);    // force load + start
        c64.Cia1.ProcessTimers(3);
        Assert.Equal(0x0010, Counter(c64));      // loaded, held
        c64.Cia1.ProcessTimers(1);
        Assert.Equal(0x000F, Counter(c64));      // first decrement four cycles after the write
    }

    [Fact]
    public void Writing_the_latch_low_byte_alone_leaves_a_stopped_counter_and_the_high_byte_loads_it()
    {
        var c64 = Build();
        Program(c64, latch: 0x1234, control: 0x00);
        Assert.Equal(0x1234, Counter(c64));

        c64.Mem.Write(CiaAddr.CIA1_TIMALO, 0x99);
        Assert.Equal(0x1234, Counter(c64));      // only the latch changed
        c64.Mem.Write(CiaAddr.CIA1_TIMAHI, 0x56);
        Assert.Equal(0x5699, Counter(c64));      // the whole latch, low byte included
    }
}
