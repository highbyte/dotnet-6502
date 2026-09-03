using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.TimerAndPeripheral;

/// <summary>
/// A CIA timer counts down once per cycle and underflows on the cycle after it reaches 0, so a
/// continuous timer with latch N has a period of N + 1 cycles; a latch of 0 gives 65,536. The
/// counter register shows the live value at any point, whether the timer is counting or stopped.
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

    [Fact]
    public void Continuous_timer_underflows_every_latch_plus_one_cycles()
    {
        var c64 = Build();
        Program(c64, latch: 3, control: 0x01);   // continuous, start

        c64.Cia1.ProcessTimers(3);
        Assert.Equal(0, Flags(c64));             // 3 cycles: 3,2,1 -> counter 0, no underflow yet
        Assert.Equal(0, c64.Cia1.TimerALOLoad(0));

        c64.Cia1.ProcessTimers(1);
        Assert.Equal(1, Flags(c64));             // 4th cycle: underflow, reload
        Assert.Equal(3, c64.Cia1.TimerALOLoad(0));

        c64.Cia1.ProcessTimers(4);
        Assert.Equal(1, Flags(c64));             // next period, another underflow
        c64.Cia1.ProcessTimers(3);
        Assert.Equal(0, Flags(c64));             // the period is 4, not 5
    }

    [Fact]
    public void One_shot_timer_stops_after_its_underflow()
    {
        var c64 = Build();
        Program(c64, latch: 1, control: 0x09);   // one-shot, start

        c64.Cia1.ProcessTimers(2);
        Assert.Equal(1, Flags(c64));
        Assert.Equal(0, c64.Cia1.TimerAControlLoad(0) & 0x01);   // start bit cleared

        c64.Cia1.ProcessTimers(100);
        Assert.Equal(0, Flags(c64));             // stopped: no further underflows
    }

    [Fact]
    public void Counter_reads_the_live_value_while_counting_and_the_frozen_value_when_stopped()
    {
        var c64 = Build();
        Program(c64, latch: 0x1000, control: 0x01);

        c64.Cia1.ProcessTimers(0x10);
        Assert.Equal(0x0FF0, c64.Cia1.TimerAHILoad(0) << 8 | c64.Cia1.TimerALOLoad(0));

        c64.Cia1.TimerAControlStore(0, 0x00);    // stop
        c64.Cia1.ProcessTimers(0x100);
        Assert.Equal(0x0FF0, c64.Cia1.TimerAHILoad(0) << 8 | c64.Cia1.TimerALOLoad(0));

        c64.Cia1.TimerAControlStore(0, 0x01);    // resume from where it stopped
        c64.Cia1.ProcessTimers(0x10);
        Assert.Equal(0x0FE0, c64.Cia1.TimerAHILoad(0) << 8 | c64.Cia1.TimerALOLoad(0));
    }

    [Fact]
    public void Force_load_restarts_from_the_latch()
    {
        var c64 = Build();
        Program(c64, latch: 0x0100, control: 0x01);
        c64.Cia1.ProcessTimers(0x80);

        c64.Cia1.TimerAControlStore(0, 0x11);    // force load + start
        Assert.Equal(0x0100, c64.Cia1.TimerAHILoad(0) << 8 | c64.Cia1.TimerALOLoad(0));

        c64.Cia1.ProcessTimers(0x101);
        Assert.Equal(1, Flags(c64));
    }
}
