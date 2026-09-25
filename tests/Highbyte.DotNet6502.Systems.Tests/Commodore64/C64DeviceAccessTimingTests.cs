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

    [Theory]
    [InlineData(0xD020, 0xD020)]   // the register itself
    [InlineData(0xD060, 0xD020)]   // a mirror: reported under the canonical address
    public void Vic2_register_write_is_reported_at_the_cycle_of_the_write(int storeAddress, int expectedRegister)
    {
        // STA abs writes on its 4th cycle: three cycles have completed, the 4th is in progress.
        var c64 = Build([0x8D, (byte)(storeAddress & 0xFF), (byte)(storeAddress >> 8)]);
        c64.CPU.A = 0x05;
        c64.Vic2.AdvanceRaster(10);
        var reported = new List<(ulong FrameCycle, ushort Register, byte Value)>();
        c64.Vic2.RegisterWriteObserver = (frameCycle, register, value) => reported.Add((frameCycle, register, value));

        Step(c64);

        var write = Assert.Single(reported);
        Assert.Equal(13UL, write.FrameCycle);
        Assert.Equal((ushort)expectedRegister, write.Register);
        Assert.Equal(0x05, write.Value);
    }

    [Fact]
    public void Cia_timer_force_load_restarts_the_count_at_the_cycle_of_the_write()
    {
        // STA $DC0E (force load + start) writes on its 4th cycle; two NOPs; LDA $DC04 reads on its
        // 4th cycle, 8 cycles after the write. The latch reaches the counter two cycles after the
        // write, holds for one more, and the counter has then moved five times by the read.
        var c64 = Build([0x8D, 0x0E, 0xDC, 0xEA, 0xEA, 0xAD, 0x04, 0xDC]);
        c64.Mem.Write(CiaAddr.CIA1_TIMALO, 62);
        c64.Mem.Write(CiaAddr.CIA1_TIMAHI, 0);
        c64.CPU.A = 0b0001_0001;

        Step(c64); Step(c64); Step(c64); Step(c64);

        Assert.Equal(62 - 5, c64.CPU.A);
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
        Assert.Equal(0x1000 - 1 & 0xFF, c64.CPU.A);   // 3 cycles completed before the first read, the first two held by the start pipeline

        Step(c64);
        Assert.Equal(0x1000 - 5 & 0xFF, c64.CPU.A);   // 7 cycles completed before the second read
    }

    [Fact]
    public void Cia_control_write_starts_the_timer_on_the_cycle_of_the_write()
    {
        // STA $DC0E writes on its 4th cycle; LDA $DC04 reads on its 4th cycle, four cycles later.
        // The counter holds through the two cycles after the write (the 6526's start pipeline),
        // so the read sees two counts.
        var c64 = Build([0x8D, 0x0E, 0xDC, 0xAD, 0x04, 0xDC]);
        c64.Mem.Write(CiaAddr.CIA1_TIMALO, 0x00);
        c64.Mem.Write(CiaAddr.CIA1_TIMAHI, 0x10);
        c64.CPU.A = 0x01;

        Step(c64);
        Step(c64);

        Assert.Equal(0x1000 - 2 & 0xFF, c64.CPU.A);
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
    public void Raster_interrupt_acknowledged_in_the_instructions_last_cycle_is_still_taken()
    {
        // The line is active and was polled at STA's second-to-last cycle; the write to $D019 in
        // its last cycle releases the line too late to stop the interrupt. The handler is entered
        // with the register already cleared.
        var c64 = Build([0xA9, 0xFF, 0x8D, 0x19, 0xD0, 0xEA]);   // LDA #$FF ; STA $D019 ; NOP
        c64.Mem.Write(0x0001, 0x35);
        c64.Mem.WriteWord(CPU.BrkIRQHandlerVector, 0x2000);
        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 1);
        c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, 0x1B);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0x01);
        c64.Vic2.AdvanceRaster(c64.Vic2.Vic2Model.CyclesPerLine);
        Assert.True(c64.CPU.IRQ);

        Step(c64);                                    // LDA, with I set: nothing taken
        c64.CPU.ProcessorStatus.InterruptDisable = false;
        var store = Step(c64);                        // STA $D019

        Assert.False(c64.CPU.IRQ);
        Assert.Equal(0x2000, c64.CPU.PC);
        Assert.Equal(4 + CPU.InterruptEntryCycles, store.CyclesConsumed);
        Assert.Equal(0x00, c64.Mem.Read(Vic2Addr.VIC_IRQ) & 0x81);
    }

    [Fact]
    public void Raster_interrupt_acknowledged_by_the_dummy_write_of_a_read_modify_write_is_still_taken()
    {
        // ASL $D019 (6 cycles): the read on cycle 4 sees the flag set, the dummy write on cycle 5
        // writes that value back and acknowledges, the write on cycle 6 stores the shifted value
        // (bit 0 clear, no acknowledge). Cycle 5 is also the poll: the CPU samples the line before
        // the write lands, so the interrupt is taken (VICE's irq-ack-vicii, the asl column).
        var c64 = Build([0x0E, 0x19, 0xD0, 0xEA]);   // ASL $D019 ; NOP
        c64.Mem.Write(0x0001, 0x35);
        c64.Mem.WriteWord(CPU.BrkIRQHandlerVector, 0x2000);
        c64.Mem.Write(Vic2Addr.CURRENT_RASTER_LINE, 1);
        c64.Mem.Write(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER, 0x1B);
        c64.Mem.Write(Vic2Addr.IRQ_MASK, 0x01);
        c64.CPU.ProcessorStatus.InterruptDisable = false;
        c64.Vic2.AdvanceRaster(c64.Vic2.Vic2Model.CyclesPerLine);
        Assert.True(c64.CPU.IRQ);

        var shift = Step(c64);                        // ASL $D019

        Assert.False(c64.CPU.IRQ);                    // the dummy write acknowledged
        Assert.Equal(0x2000, c64.CPU.PC);             // and the interrupt was taken anyway
        Assert.Equal(6 + CPU.InterruptEntryCycles, shift.CyclesConsumed);
    }

    [Fact]
    public void Cia_timer_interrupt_is_dated_to_the_underflow_cycle()
    {
        // Timer A latch 5, started by a direct write: the counter holds for two cycles, then
        // underflows after 6 counted cycles, i.e. during cycle 8 of the program below. Four NOPs =
        // cycles 1-8: the underflow falls on the last cycle of the fourth NOP, so the IRQ is taken
        // after the fifth.
        var c64 = Build([0xEA, 0xEA, 0xEA, 0xEA, 0xEA]);
        c64.Mem.Write(0x0001, 0x35);
        c64.Mem.WriteWord(CPU.BrkIRQHandlerVector, 0x2000);
        c64.CPU.ProcessorStatus.InterruptDisable = false;
        c64.Mem.Write(CiaAddr.CIA1_TIMALO, 0x05);
        c64.Mem.Write(CiaAddr.CIA1_TIMAHI, 0x00);
        c64.Mem.Write(CiaAddr.CIA1_CIAICR, 0x81);      // enable timer A interrupt
        c64.Mem.Write(CiaAddr.CIA1_CIACRA, 0x09);   // one-shot, start

        Step(c64); Step(c64); Step(c64);
        var fourth = Step(c64);
        Assert.Equal(2UL, fourth.CyclesConsumed);
        Assert.Equal(Start + 4, c64.CPU.PC);          // underflow on the last cycle: not yet
        Assert.True(c64.CPU.IRQ);

        var fifth = Step(c64);
        Assert.Equal(2 + CPU.InterruptEntryCycles, fifth.CyclesConsumed);
        Assert.Equal(0x2000, c64.CPU.PC);
    }

    [Fact]
    public void Enabling_a_cia_interrupt_whose_flag_is_set_is_seen_by_the_second_poll_after_the_write()
    {
        // Timer A latch 0 underflows as soon as it starts, so its flag is set by the time the mask
        // is written. The output follows a cycle after the write and the CPU sees it a cycle after
        // that: not at the poll of the instruction after the write (Lorenz's imr "clock 2"), but at
        // the poll of the one after (imr "clock 3").
        var c64 = Build([0xA9, 0x19, 0x8D, 0x0E, 0xDC, 0xA9, 0x81, 0x8D, 0x0D, 0xDC, 0xEA, 0xEA, 0xEA]);   // LDA #$19 ; STA $DC0E ; LDA #$81 ; STA $DC0D ; NOP ; NOP ; NOP
        c64.Mem.Write(0x0001, 0x35);
        c64.Mem.WriteWord(CPU.BrkIRQHandlerVector, 0x2000);
        c64.CPU.ProcessorStatus.InterruptDisable = false;
        c64.Mem.Write(CiaAddr.CIA1_TIMALO, 0x00);
        c64.Mem.Write(CiaAddr.CIA1_TIMAHI, 0x00);
        Step(c64); Step(c64);                         // start with force load: the timer underflows at once
        Step(c64);                                    // LDA #$81

        var store = Step(c64);                        // STA $DC0D: enables timer A
        Assert.Equal(4UL, store.CyclesConsumed);
        var first = Step(c64);                        // NOP: its poll is the cycle after the write
        Assert.Equal(2UL, first.CyclesConsumed);
        Assert.Equal(Start + 11, c64.CPU.PC);
        var second = Step(c64);                       // NOP: the line is seen now
        Assert.Equal(2 + CPU.InterruptEntryCycles, second.CyclesConsumed);
        Assert.Equal(0x2000, c64.CPU.PC);
    }

    [Theory]
    [InlineData(new byte[] { 0xEA, 0xA9, 0x02, 0x8D, 0x0D, 0xDC }, true)]    // NOP ; LDA #$02 ; STA $DC0D: the write is cycle 8, the cycle the output is due
    [InlineData(new byte[] { 0x24, 0x01, 0x8D, 0x0D, 0xDC, 0xEA }, false)]   // BIT $01 ; STA $DC0D ; NOP: the write is cycle 7, a cycle before
    public void Disabling_timer_B_interrupt_in_the_cycle_its_output_is_due_still_drives_the_output(byte[] program, bool interruptTaken)
    {
        // The write lands at the end of its cycle: the interrupt output due in that cycle is
        // computed with the mask as it was (VICE's cia-int, last column, timer B). Timer B latch 5
        // started by a direct write underflows during cycle 7 of the program; its output is due in
        // cycle 8 and, enabled, is seen by the poll at cycle 9. A disable written on cycle 8 comes
        // too late to stop it; one written on cycle 7 does.
        var c64 = Build(program.Concat(new byte[] { 0xEA, 0xEA }).ToArray());
        c64.Mem.Write(0x0001, 0x35);
        c64.Mem.WriteWord(CPU.BrkIRQHandlerVector, 0x2000);
        c64.CPU.ProcessorStatus.InterruptDisable = false;
        c64.CPU.A = 0x02;                             // what the STA writes: clear the timer B mask bit
        c64.Mem.Write(CiaAddr.CIA1_TIMBLO, 0x05);
        c64.Mem.Write(CiaAddr.CIA1_TIMBHI, 0x00);
        c64.Mem.Write(CiaAddr.CIA1_CIAICR, 0x82);
        c64.Mem.Write(CiaAddr.CIA1_CIACRB, 0x09);
        Step(c64); Step(c64);
        Step(c64);                                    // cycles 1-8 in both programs: the STA's write cycle passed

        var next = Step(c64);                         // NOP with its poll at cycle 9
        Assert.Equal(interruptTaken, c64.CPU.IRQ);
        Assert.Equal(interruptTaken ? 2 + CPU.InterruptEntryCycles : 2UL, next.CyclesConsumed);
        Assert.Equal(interruptTaken ? 0x2000 : Start + program.Length + 1, c64.CPU.PC);
    }

    [Fact]
    public void An_instruction_running_past_the_frame_end_carries_its_remainder_into_the_next_frame()
    {
        // NOP (2 cycles) starting on the frame's last cycle: one cycle belongs to the new frame.
        var c64 = Build([0xEA]);
        var cyclesPerFrame = c64.Vic2.Vic2Model.CyclesPerFrame;
        c64.Vic2.AdvanceRaster(cyclesPerFrame - 1);

        Step(c64);

        Assert.Equal(1UL, c64.Vic2.CyclesConsumedCurrentVblank);
        Assert.Equal(0, c64.Vic2.CurrentRasterLine);
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
