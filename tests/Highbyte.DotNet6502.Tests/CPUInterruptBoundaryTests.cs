using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// System-independent tests for the CPU interrupt-boundary contract that systems rely on
/// when they tick devices after an instruction and then flush newly raised hardware
/// interrupts via <see cref="CPU.ProcessPendingInterrupts(Memory)"/>.
///
/// These cover the universal 6502 semantics (edge-triggered NMI, level-triggered IRQ,
/// NMI &gt; IRQ priority) plus the post-device-tick servicing point and the
/// <see cref="CPU.NmiAcknowledging"/> boundary that C64 expansion-port hardware depends on.
/// They are intentionally not tied to any emulated system.
/// </summary>
public class CPUInterruptBoundaryTests
{
    // Helper: place a NOP at the given address so the only PC change in a test comes from
    // interrupt servicing, not from a multi-byte/branching instruction.
    private static (CPU cpu, Memory mem) NewCpuAt(ushort pc)
    {
        var cpu = new CPU();
        var mem = new Memory();
        mem[pc] = (byte)OpCodeId.NOP;
        cpu.PC = pc;
        cpu.SP = 0xFF;
        return (cpu, mem);
    }

    [Fact]
    public void Interrupt_Raised_By_PostInstruction_DeviceTick_Is_Serviced_On_The_Next_Boundary()
    {
        // Models the C64 pipeline: execute one instruction, then a device tick raises an
        // IRQ, then the system flushes pending interrupts. The IRQ must land here -- before
        // the instruction after the NOP runs -- not one instruction late.
        var (cpu, mem) = NewCpuAt(0x1000);
        cpu.ProcessorStatus.InterruptDisable = false;
        mem.WriteWord(CPU.BrkIRQHandlerVector, 0x4000);

        // Put a distinctive instruction right after the NOP. If the IRQ were serviced one
        // instruction late, this would execute and advance PC past 0x1001 first.
        mem[0x1001] = (byte)OpCodeId.NOP;

        cpu.ExecuteOneInstructionMinimal(mem);   // NOP at 0x1000 -> PC = 0x1001
        Assert.Equal((ushort)0x1001, cpu.PC);

        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true); // device tick
        cpu.ProcessPendingInterrupts(mem);

        // PC is the IRQ handler, so the next instruction fetched is the handler -- the
        // instruction at 0x1001 was not executed.
        Assert.Equal((ushort)0x4000, cpu.PC);
        Assert.False(cpu.IRQ); // auto-acknowledged source cleared
    }

    [Fact]
    public void IRQ_Raised_Between_Instructions_Is_Serviced_By_ProcessPendingInterrupts()
    {
        var (cpu, mem) = NewCpuAt(0x1000);
        cpu.ProcessorStatus.InterruptDisable = false;
        mem.WriteWord(CPU.BrkIRQHandlerVector, 0x4000);

        cpu.ExecuteOneInstructionMinimal(mem);
        Assert.Equal((ushort)0x1001, cpu.PC);

        cpu.CPUInterrupts.SetIRQSourceActive("dummy", autoAcknowledge: true);
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal((ushort)0x4000, cpu.PC);

        // The pre-interrupt PC (0x1001) was pushed so RTI returns to it.
        var pcOnStackAddress = (ushort)(CPU.StackBaseAddress + (byte)(cpu.SP + 2));
        var pcOnStack = ByteHelpers.ToLittleEndianWord(
            new[] { mem[pcOnStackAddress], mem[(ushort)(pcOnStackAddress + 1)] });
        Assert.Equal((ushort)0x1001, pcOnStack);
    }

    [Fact]
    public void IRQ_Raised_Between_Instructions_Is_Held_Off_While_InterruptDisable_Is_Set()
    {
        // IRQ is level-triggered and masked by the I flag: a source raised between
        // instructions must not be serviced until the I flag is cleared, and then it is
        // serviced on the next flush because the line is still held active.
        var (cpu, mem) = NewCpuAt(0x1000);
        cpu.ProcessorStatus.InterruptDisable = true;
        mem.WriteWord(CPU.BrkIRQHandlerVector, 0x4000);

        cpu.ExecuteOneInstructionMinimal(mem);
        cpu.CPUInterrupts.SetIRQSourceActive("dummy", autoAcknowledge: false);
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal((ushort)0x1001, cpu.PC);   // masked -> not serviced
        Assert.True(cpu.IRQ);                    // line still held active

        cpu.ProcessorStatus.InterruptDisable = false;
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal((ushort)0x4000, cpu.PC);    // serviced once unmasked
    }

    [Fact]
    public void NmiAcknowledging_Fires_Before_The_Nmi_Vector_Is_Read()
    {
        // C64 expansion-port hardware (e.g. Final Cartridge III / Expert) changes memory
        // mapping as part of NMI acknowledgement so the correct vector becomes visible. The
        // CPU exposes NmiAcknowledging precisely so a system can remap before the vector is
        // fetched. Proven here by swapping the vector inside the handler and confirming the
        // CPU jumps to the swapped value.
        var (cpu, mem) = NewCpuAt(0x1000);
        mem.WriteWord(CPU.NonMaskableIRQHandlerVector, 0x1111);

        int fireCount = 0;
        cpu.NmiAcknowledging += (_, _) =>
        {
            fireCount++;
            mem.WriteWord(CPU.NonMaskableIRQHandlerVector, 0x2222);
        };

        cpu.ExecuteOneInstructionMinimal(mem);
        cpu.CPUInterrupts.SetNMISourceActive("cart");
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal(1, fireCount);
        Assert.Equal((ushort)0x2222, cpu.PC); // jumped to the vector exposed during acknowledge
    }

    [Fact]
    public void NmiAcknowledging_Does_Not_Fire_When_No_Nmi_Is_Pending()
    {
        var (cpu, mem) = NewCpuAt(0x1000);

        int fireCount = 0;
        cpu.NmiAcknowledging += (_, _) => fireCount++;

        cpu.ExecuteOneInstructionMinimal(mem);
        cpu.ProcessPendingInterrupts(mem); // nothing pending

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void NMI_Is_Edge_Triggered_And_Does_Not_Retrigger_While_Source_Stays_Active()
    {
        // A held-low NMI line must fire exactly once. Re-asserting an already-active source
        // does not produce a new edge; the source must be released and re-asserted.
        var (cpu, mem) = NewCpuAt(0x1000);
        mem.WriteWord(CPU.NonMaskableIRQHandlerVector, 0x4000);

        cpu.CPUInterrupts.SetNMISourceActive("cart");
        cpu.ProcessPendingInterrupts(mem);
        Assert.Equal((ushort)0x4000, cpu.PC);
        Assert.False(cpu.NMI); // pending edge consumed

        // Source is still active, but no new falling edge -> no second NMI.
        cpu.PC = 0x2000;
        cpu.CPUInterrupts.SetNMISourceActive("cart"); // re-assert already-active source: no edge
        cpu.ProcessPendingInterrupts(mem);
        Assert.Equal((ushort)0x2000, cpu.PC);
        Assert.False(cpu.NMI);

        // Release then re-assert -> new edge -> serviced again.
        cpu.CPUInterrupts.SetNMISourceInactive("cart");
        cpu.CPUInterrupts.SetNMISourceActive("cart");
        Assert.True(cpu.NMI);
        cpu.ProcessPendingInterrupts(mem);
        Assert.Equal((ushort)0x4000, cpu.PC);
    }

    [Fact]
    public void NMI_Takes_Priority_Over_IRQ_When_Both_Are_Pending()
    {
        var (cpu, mem) = NewCpuAt(0x1000);
        cpu.ProcessorStatus.InterruptDisable = false;
        mem.WriteWord(CPU.BrkIRQHandlerVector, 0x4000);
        mem.WriteWord(CPU.NonMaskableIRQHandlerVector, 0x5000);

        cpu.CPUInterrupts.SetIRQSourceActive("irq", autoAcknowledge: true);
        cpu.CPUInterrupts.SetNMISourceActive("nmi");
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal((ushort)0x5000, cpu.PC); // NMI handler, not IRQ handler
        Assert.True(cpu.IRQ);                  // IRQ still pending, serviced on a later flush
    }
}
