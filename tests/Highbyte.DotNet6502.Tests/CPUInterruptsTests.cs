using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Contract of the bitmask-backed <see cref="CPUInterrupts"/>: named sources map to line bits,
/// IRQ is level-triggered with per-source acknowledge mode, NMI is edge-latched, and the
/// string-based API is a façade over the handle-based one.
/// </summary>
public class CPUInterruptsTests
{
    [Fact]
    public void GetSource_Registers_Once_And_Returns_A_Stable_Handle()
    {
        var interrupts = new CPUInterrupts();

        var first = interrupts.GetSource("VIC2.Raster");
        var second = interrupts.GetSource("VIC2.Raster");
        var other = interrupts.GetSource("CIA1.TimerA");

        Assert.Equal(first, second);
        Assert.Equal(0, first.Index);
        Assert.Equal(1, other.Index);
        Assert.Equal(1UL << 1, other.Mask);
        Assert.Equal(2, interrupts.RegisteredSourceCount);
    }

    [Fact]
    public void TryGetSource_Does_Not_Register_Unknown_Names()
    {
        var interrupts = new CPUInterrupts();

        Assert.False(interrupts.TryGetSource("nope", out _));
        Assert.False(interrupts.IsIRQSourceActive("nope"));
        Assert.False(interrupts.IsNMISourceActive("nope"));
        interrupts.SetIRQSourceInactive("nope");
        interrupts.SetNMISourceInactive("nope");

        Assert.Equal(0, interrupts.RegisteredSourceCount);
    }

    [Fact]
    public void Registering_More_Than_MaxSources_Throws()
    {
        var interrupts = new CPUInterrupts();
        for (var i = 0; i < CPUInterrupts.MaxSources; i++)
            interrupts.GetSource($"source-{i}");

        Assert.Throws<InvalidOperationException>(() => interrupts.GetSource("one-too-many"));
    }

    [Fact]
    public void IRQ_Line_Is_Asserted_While_Any_Source_Is_Active()
    {
        var interrupts = new CPUInterrupts();
        Assert.False(interrupts.IRQLineEnabled);

        interrupts.SetIRQSourceActive("a", autoAcknowledge: false);
        interrupts.SetIRQSourceActive("b", autoAcknowledge: true);
        Assert.True(interrupts.IRQLineEnabled);

        interrupts.SetIRQSourceInactive("a");
        Assert.True(interrupts.IRQLineEnabled);
        Assert.False(interrupts.IsIRQSourceActive("a"));
        Assert.True(interrupts.IsIRQSourceActive("b"));

        interrupts.SetIRQSourceInactive("b");
        Assert.False(interrupts.IRQLineEnabled);
    }

    [Fact]
    public void Servicing_Drops_Only_AutoAcknowledging_IRQ_Sources()
    {
        var interrupts = new CPUInterrupts();
        var manual = interrupts.GetSource("manual");
        var auto = interrupts.GetSource("auto");
        interrupts.SetIRQActive(manual, autoAcknowledge: false);
        interrupts.SetIRQActive(auto, autoAcknowledge: true);

        interrupts.AcknowledgeAutoAcknowledgingIRQSources();

        Assert.True(interrupts.IsIRQActive(manual));
        Assert.False(interrupts.IsIRQActive(auto));
        Assert.False(interrupts.IsIRQAutoAcknowledged(auto));
        Assert.True(interrupts.IRQLineEnabled);
    }

    [Fact]
    public void Reasserting_An_Active_IRQ_Source_Keeps_Its_Acknowledge_Mode()
    {
        var interrupts = new CPUInterrupts();
        var source = interrupts.GetSource("device");
        interrupts.SetIRQActive(source, autoAcknowledge: false);

        interrupts.SetIRQActive(source, autoAcknowledge: true);

        Assert.False(interrupts.IsIRQAutoAcknowledged(source));
        interrupts.AcknowledgeAutoAcknowledgingIRQSources();
        Assert.True(interrupts.IsIRQActive(source));
    }

    [Fact]
    public void Clearing_An_IRQ_Source_Also_Clears_Its_Acknowledge_Mode()
    {
        var interrupts = new CPUInterrupts();
        var source = interrupts.GetSource("device");
        interrupts.SetIRQActive(source, autoAcknowledge: true);

        interrupts.SetIRQInactive(source);
        interrupts.SetIRQActive(source, autoAcknowledge: false);

        Assert.False(interrupts.IsIRQAutoAcknowledged(source));
    }

    [Fact]
    public void NMI_Is_Latched_On_The_Inactive_To_Active_Edge_Only()
    {
        var interrupts = new CPUInterrupts();

        interrupts.SetNMISourceActive("restore");
        Assert.True(interrupts.NMIPending);
        Assert.True(interrupts.NMILineEnabled);

        interrupts.ClearPendingNMI();
        interrupts.SetNMISourceActive("restore"); // still held active: no new edge
        Assert.False(interrupts.NMIPending);

        interrupts.SetNMISourceInactive("restore");
        Assert.False(interrupts.NMILineEnabled);
        interrupts.SetNMISourceActive("restore");
        Assert.True(interrupts.NMIPending);
    }

    [Fact]
    public void Active_Source_Enumerations_Report_Names_And_Acknowledge_Modes()
    {
        var interrupts = new CPUInterrupts();
        interrupts.SetIRQSourceActive("irq-manual", autoAcknowledge: false);
        interrupts.SetIRQSourceActive("irq-auto", autoAcknowledge: true);
        interrupts.SetIRQSourceActive("irq-cleared", autoAcknowledge: true);
        interrupts.SetIRQSourceInactive("irq-cleared");
        interrupts.SetNMISourceActive("nmi-a");
        interrupts.SetNMISourceActive("nmi-b");
        interrupts.SetNMISourceInactive("nmi-a");

        Assert.Equal(
            [new("irq-manual", false), new("irq-auto", true)],
            interrupts.ActiveIRQSources.ToArray());
        Assert.Equal(["nmi-b"], interrupts.ActiveNMISources.ToArray());
    }

    [Fact]
    public void ClearAll_Deasserts_Lines_But_Keeps_Registered_Handles_Valid()
    {
        var interrupts = new CPUInterrupts();
        var irq = interrupts.GetSource("irq");
        var nmi = interrupts.GetSource("nmi");
        interrupts.SetIRQActive(irq, autoAcknowledge: true);
        interrupts.SetNMIActive(nmi);

        interrupts.ClearAll();

        Assert.False(interrupts.IRQLineEnabled);
        Assert.False(interrupts.NMILineEnabled);
        Assert.False(interrupts.NMIPending);
        Assert.Equal(2, interrupts.RegisteredSourceCount);
        Assert.Equal(irq, interrupts.GetSource("irq"));

        interrupts.SetIRQActive(irq, autoAcknowledge: false);
        Assert.True(interrupts.IsIRQSourceActive("irq"));
        Assert.False(interrupts.IsIRQAutoAcknowledged(irq));
    }

    [Fact]
    public void Cpu_Servicing_An_IRQ_Keeps_Manually_Acknowledged_Sources_Asserted()
    {
        var cpu = new CPU();
        var mem = new Memory();
        mem[0x1000] = (byte)OpCodeId.NOP;
        cpu.PC = 0x1000;
        cpu.ProcessorStatus.InterruptDisable = false;
        mem.WriteWord(CPU.BrkIRQHandlerVector, 0x4000);
        var manual = cpu.CPUInterrupts.GetSource("VIC2.RasterCompare");
        var auto = cpu.CPUInterrupts.GetSource("CIA1.TimerA");
        cpu.CPUInterrupts.SetIRQActive(manual, autoAcknowledge: false);
        cpu.CPUInterrupts.SetIRQActive(auto, autoAcknowledge: true);

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(2 + CPU.InterruptEntryCycles, result.CyclesConsumed);
        Assert.Equal((ushort)0x4000, cpu.PC);
        Assert.True(cpu.CPUInterrupts.IsIRQActive(manual));
        Assert.False(cpu.CPUInterrupts.IsIRQActive(auto));
        Assert.True(cpu.CPUInterrupts.IRQLineEnabled);
    }
}
