using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

/// <summary>
/// Instruction-boundary snapshots carry the CPU's interrupt lines by source name with their
/// acknowledge mode, plus the latched NMI edge — independent of the bit positions the sources
/// happen to occupy on either machine.
/// </summary>
public class CpuInterruptSnapshotTests
{
    private static GenericComputer BuildComputer()
    {
        var computer = new GenericComputer(new GenericComputerConfig(), new NullLoggerFactory());
        computer.Mem.StoreData(0xC000, [0xEA, 0x4C, 0x00, 0xC0]); // NOP; JMP $C000
        computer.CPU.PC = 0xC000;
        return computer;
    }

    private static GenericComputer RoundTrip(GenericComputer source, Action<GenericComputer>? prepareTarget = null)
    {
        using var stream = new MemoryStream();
        new SnapshotService().Save(source, stream);
        stream.Position = 0;

        var target = BuildComputer();
        prepareTarget?.Invoke(target);
        new SnapshotService().Restore(target, stream);
        return target;
    }

    [Fact]
    public void Interrupt_Lines_And_Acknowledge_Modes_Round_Trip()
    {
        var source = BuildComputer();
        source.CPU.ProcessorStatus.InterruptDisable = true; // keep the lines asserted across the save
        source.CPU.CPUInterrupts.SetIRQSourceActive("VIC2.RasterCompare", autoAcknowledge: false);
        source.CPU.CPUInterrupts.SetIRQSourceActive("CIA1.TimerA", autoAcknowledge: true);
        source.CPU.CPUInterrupts.SetNMISourceActive("Keyboard.Restore");

        var restored = RoundTrip(source);
        var interrupts = restored.CPU.CPUInterrupts;

        Assert.Equal(
            [new("VIC2.RasterCompare", false), new("CIA1.TimerA", true)],
            interrupts.ActiveIRQSources.ToArray());
        Assert.Equal(["Keyboard.Restore"], interrupts.ActiveNMISources.ToArray());
        Assert.True(interrupts.NMIPending);
    }

    [Fact]
    public void Restore_Uses_Names_Not_Bit_Positions()
    {
        var source = BuildComputer();
        source.CPU.CPUInterrupts.SetIRQSourceActive("second", autoAcknowledge: true);

        // The target registered other sources first, so "second" lands on a different bit.
        var restored = RoundTrip(source, target =>
        {
            target.CPU.CPUInterrupts.GetSource("first");
            target.CPU.CPUInterrupts.GetSource("third");
        });

        var handle = restored.CPU.CPUInterrupts.GetSource("second");
        Assert.Equal(2, handle.Index);
        Assert.True(restored.CPU.CPUInterrupts.IsIRQActive(handle));
        Assert.True(restored.CPU.CPUInterrupts.IsIRQAutoAcknowledged(handle));
    }

    [Fact]
    public void Restore_Replaces_The_Targets_Existing_Line_State()
    {
        var source = BuildComputer(); // nothing asserted

        var restored = RoundTrip(source, target =>
        {
            target.CPU.CPUInterrupts.SetIRQSourceActive("stale", autoAcknowledge: false);
            target.CPU.CPUInterrupts.SetNMISourceActive("stale-nmi");
        });

        Assert.False(restored.CPU.CPUInterrupts.IRQLineEnabled);
        Assert.False(restored.CPU.CPUInterrupts.NMILineEnabled);
        Assert.False(restored.CPU.CPUInterrupts.NMIPending);
    }

    [Fact]
    public void A_Serviced_Nmi_Edge_Stays_Serviced_After_Restore()
    {
        var source = BuildComputer();
        source.CPU.CPUInterrupts.SetNMISourceActive("held");
        source.CPU.CPUInterrupts.ClearPendingNMI(); // edge already taken, source still held active

        var restored = RoundTrip(source);

        Assert.True(restored.CPU.CPUInterrupts.NMILineEnabled);
        Assert.False(restored.CPU.CPUInterrupts.NMIPending);
    }
}
