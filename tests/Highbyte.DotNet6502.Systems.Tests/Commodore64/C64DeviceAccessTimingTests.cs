using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

/// <summary>
/// VIC-II and CIA register accesses see the device state at the CPU bus cycle of the access,
/// not at the previous instruction boundary. An absolute read (<c>LDA abs</c>, 4 cycles) reads
/// on its 4th cycle, so three cycles have completed when the device is consulted; an indirect
/// indexed read (<c>LDA (zp),Y</c>, 5 cycles) reads on its 5th.
/// </summary>
public class C64DeviceAccessTimingTests
{
    private const ushort Start = 0x1000;

    private static C64 Build(byte[] program, TimerMode timerMode = TimerMode.UpdateEachRasterLine)
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
            TimerMode = timerMode,
        }, NullLoggerFactory.Instance);
        c64.Mem.StoreData(Start, program);
        c64.CPU.PC = Start;
        return c64;
    }

    private static InstructionExecResult Step(C64 c64)
    {
        c64.ExecuteOneInstruction(out var result);
        return result;
    }

    [Theory]
    [InlineData(3, 1)]   // line changes on the read cycle itself: the read sees the new line
    [InlineData(4, 0)]   // line changes one cycle after the read: the read sees the old line
    public void Raster_register_read_sees_the_line_at_the_cycle_of_the_read(int cyclesToLineChangeAtStart, int expectedLine)
    {
        var c64 = Build([0xAD, 0x12, 0xD0]);   // LDA $D012
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(cyclesPerLine - (ulong)cyclesToLineChangeAtStart);

        var result = Step(c64);

        Assert.Equal(expectedLine, c64.CPU.A);
        Assert.Equal(4UL, result.CyclesConsumed);
        Assert.Equal(cyclesPerLine - (ulong)cyclesToLineChangeAtStart + 4, c64.Vic2.CyclesConsumedCurrentVblank);
    }

    [Fact]
    public void Raster_read_cycle_depends_on_the_instruction_shape()
    {
        // LDA ($FB),Y with Y=0 reads on its 5th cycle, one cycle later than LDA abs.
        var c64 = Build([0xB1, 0xFB]);
        c64.Mem.WriteWord(0x00FB, Vic2Addr.CURRENT_RASTER_LINE);
        c64.CPU.Y = 0;
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(cyclesPerLine - 4);

        var result = Step(c64);

        Assert.Equal(1, c64.CPU.A);            // LDA abs from the same position sees line 0 (theory above)
        Assert.Equal(5UL, result.CyclesConsumed);
    }

    [Theory]
    [InlineData(TimerMode.UpdateEachRasterLine)]
    [InlineData(TimerMode.UpdateEachInstruction)]
    public void Cia_timer_read_sees_the_count_at_the_cycle_of_the_read(TimerMode timerMode)
    {
        // LDA $DC04 ; LDA $DC04 — each reads on its 4th cycle.
        var c64 = Build([0xAD, 0x04, 0xDC, 0xAD, 0x04, 0xDC], timerMode);
        c64.Mem.Write(CiaAddr.CIA1_TIMALO, 0x00);
        c64.Mem.Write(CiaAddr.CIA1_TIMAHI, 0x10);
        c64.Mem.Write(CiaAddr.CIA1_CIACRA, 0x01);   // start, continuous

        Step(c64);
        Assert.Equal(0x1000 - 3 & 0xFF, c64.CPU.A);   // 3 cycles completed before the first read

        Step(c64);
        Assert.Equal(0x1000 - 7 & 0xFF, c64.CPU.A);   // 7 cycles completed before the second read
    }

    [Fact]
    public void Cia_control_write_starts_the_timer_on_the_cycle_of_the_write()
    {
        // STA $DC0E writes on its 4th cycle; LDA $DC04 reads on its 4th cycle, four cycles later.
        // The timer counts from the write's own cycle, so the read sees four counts. (The 6526's
        // start-up pipeline delay is not modelled.)
        var c64 = Build([0x8D, 0x0E, 0xDC, 0xAD, 0x04, 0xDC]);
        c64.Mem.Write(CiaAddr.CIA1_TIMALO, 0x00);
        c64.Mem.Write(CiaAddr.CIA1_TIMAHI, 0x10);
        c64.CPU.A = 0x01;

        Step(c64);
        Step(c64);

        Assert.Equal(0x1000 - 4 & 0xFF, c64.CPU.A);
    }

    [Fact]
    public void Raster_interrupt_that_becomes_due_during_an_instruction_is_serviced_right_after_it()
    {
        var c64 = Build([0xAD, 0x12, 0xD0, 0xEA]);   // LDA $D012 ; NOP
        c64.Mem.Write(0x0001, 0x35);                  // RAM under the KERNAL, I/O visible
        c64.Mem.WriteWord(CPU.BrkIRQHandlerVector, 0x2000);
        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 1);
        c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, 0x1B);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0x01);
        c64.CPU.ProcessorStatus.InterruptDisable = false;
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(cyclesPerLine - 3);    // line 1 begins on the read cycle

        var result = Step(c64);

        Assert.Equal(1, c64.CPU.A);
        Assert.Equal(0x2000, c64.CPU.PC);
        Assert.Equal(4 + CPU.InterruptEntryCycles, result.CyclesConsumed);
        Assert.Equal(cyclesPerLine - 3 + 4 + CPU.InterruptEntryCycles, c64.Vic2.CyclesConsumedCurrentVblank);
    }

    [Fact]
    public void Devices_advance_by_exactly_the_instruction_cycles_after_a_snapshot_restore()
    {
        var source = Build([0xEA, 0xEA, 0xEA, 0xEA, 0xEA, 0xEA, 0xEA, 0xEA]);
        for (var i = 0; i < 3; i++)
            Step(source);

        using var stream = new MemoryStream();
        new SnapshotService().Save(source, stream);
        stream.Position = 0;

        // The target has run further than the source, so its bus-cycle counter is ahead.
        var target = Build([0xEA, 0xEA, 0xEA, 0xEA, 0xEA, 0xEA, 0xEA, 0xEA]);
        for (var i = 0; i < 6; i++)
            Step(target);
        new SnapshotService().Restore(target, stream);
        var rasterAfterRestore = target.Vic2.CyclesConsumedCurrentVblank;
        Assert.Equal(source.Vic2.CyclesConsumedCurrentVblank, rasterAfterRestore);

        var result = Step(target);

        Assert.Equal(rasterAfterRestore + result.CyclesConsumed, target.Vic2.CyclesConsumedCurrentVblank);
    }
}
