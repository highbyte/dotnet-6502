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

    private static C64 Build(byte[] program)
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL",
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

    [Fact]
    public void Cia_timer_read_sees_the_count_at_the_cycle_of_the_read()
    {
        // LDA $DC04 ; LDA $DC04 — each reads on its 4th cycle.
        var c64 = Build([0xAD, 0x04, 0xDC, 0xAD, 0x04, 0xDC]);
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

    [Theory]
    [InlineData(new byte[] { 0xAD, 0x12, 0xD0 }, 2, true)]    // LDA $D012: line 1 begins on cycle 3 (second-to-last) -> taken after this instruction
    [InlineData(new byte[] { 0xAD, 0x12, 0xD0 }, 3, false)]   // LDA $D012: line 1 begins on cycle 4 (last) -> taken after the next instruction
    [InlineData(new byte[] { 0xAD, 0x00, 0x10 }, 2, true)]    // LDA $1000 (no I/O): the boundary catch-up dates the line change to cycle 3 -> taken now
    [InlineData(new byte[] { 0xAD, 0x00, 0x10 }, 3, false)]   // LDA $1000: line change on the last cycle -> next boundary
    public void Raster_interrupt_is_taken_after_the_instruction_only_if_due_by_its_second_to_last_cycle(byte[] instruction, int cyclesToLineChangeAtStart, bool takenAfterThisInstruction)
    {
        var program = instruction.Concat(new byte[] { 0xEA }).ToArray();   // ... ; NOP
        var c64 = Build(program);
        c64.Mem.Write(0x0001, 0x35);                  // RAM under the KERNAL, I/O visible
        c64.Mem.WriteWord(CPU.BrkIRQHandlerVector, 0x2000);
        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 1);
        c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, 0x1B);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0x01);
        c64.CPU.ProcessorStatus.InterruptDisable = false;
        var cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;
        c64.Vic2.AdvanceRaster(cyclesPerLine - (ulong)cyclesToLineChangeAtStart);

        var first = Step(c64);

        Assert.True(c64.CPU.IRQ);   // the line is asserted either way
        if (takenAfterThisInstruction)
        {
            Assert.Equal(0x2000, c64.CPU.PC);
            Assert.Equal(4 + CPU.InterruptEntryCycles, first.CyclesConsumed);
        }
        else
        {
            Assert.Equal(Start + 3, c64.CPU.PC);
            Assert.Equal(4UL, first.CyclesConsumed);

            var second = Step(c64);   // the NOP runs, then the interrupt is taken
            Assert.Equal(0x2000, c64.CPU.PC);
            Assert.Equal(2 + CPU.InterruptEntryCycles, second.CyclesConsumed);
        }
    }

    [Fact]
    public void Cia_timer_interrupt_is_dated_to_the_underflow_cycle()
    {
        // Timer A latch 5, started by a direct write: it underflows after 6 counted cycles, i.e.
        // during cycle 6 of the program below. NOP NOP NOP = cycles 1-2, 3-4, 5-6: the underflow
        // falls on the last cycle of the third NOP, so the IRQ is taken after the fourth.
        var c64 = Build([0xEA, 0xEA, 0xEA, 0xEA, 0xEA]);
        c64.Mem.Write(0x0001, 0x35);
        c64.Mem.WriteWord(CPU.BrkIRQHandlerVector, 0x2000);
        c64.CPU.ProcessorStatus.InterruptDisable = false;
        c64.Mem.Write(CiaAddr.CIA1_TIMALO, 0x05);
        c64.Mem.Write(CiaAddr.CIA1_TIMAHI, 0x00);
        c64.Mem.Write(CiaAddr.CIA1_CIAICR, 0x81);      // enable timer A interrupt
        c64.Mem.Write(CiaAddr.CIA1_CIACRA, 0x09);   // one-shot, start

        Step(c64); Step(c64);
        var third = Step(c64);
        Assert.Equal(2UL, third.CyclesConsumed);
        Assert.Equal(Start + 3, c64.CPU.PC);          // underflow on the last cycle: not yet
        Assert.True(c64.CPU.IRQ);

        var fourth = Step(c64);
        Assert.Equal(2 + CPU.InterruptEntryCycles, fourth.CyclesConsumed);
        Assert.Equal(0x2000, c64.CPU.PC);
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
