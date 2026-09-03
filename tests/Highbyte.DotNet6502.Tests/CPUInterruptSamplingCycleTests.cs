using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// The 6502 samples its IRQ and NMI inputs at the end of an instruction's second-to-last cycle
/// (a taken branch that does not cross a page: at the end of its first cycle). A line that goes
/// active during the last cycle is therefore only seen after the following instruction. Devices
/// report the bus cycle they asserted the line on; a source set active without a cycle is taken
/// at the next boundary as before. CLI, SEI and PLP change the I flag after the poll, RTI before it.
/// </summary>
public class CPUInterruptSamplingCycleTests
{
    private const ushort Start = 0x1000;
    private const ushort IrqHandler = 0x4000;
    private const ushort NmiHandler = 0x5000;

    private static (CPU cpu, Memory mem) NewCpu(params byte[] program)
    {
        var cpu = new CPU();
        var mem = new Memory();
        mem.StoreData(Start, program);
        mem.WriteWord(CPU.BrkIRQHandlerVector, IrqHandler);
        mem.WriteWord(CPU.NonMaskableIRQHandlerVector, NmiHandler);
        cpu.PC = Start;
        cpu.SP = 0xFF;
        cpu.ProcessorStatus.InterruptDisable = false;
        return (cpu, mem);
    }

    [Theory]
    [InlineData(1, true)]    // asserted during cycle 1 of a 2-cycle NOP (second-to-last): taken after it
    [InlineData(2, false)]   // asserted during cycle 2 (last): taken after the next instruction
    public void IRQ_is_taken_after_an_instruction_only_if_asserted_by_its_second_to_last_cycle(ulong assertedAtBusCycle, bool takenNow)
    {
        var (cpu, mem) = NewCpu(0xEA, 0xEA);   // NOP ; NOP

        cpu.ExecuteOneInstructionMinimal(mem);                      // bus cycles 1-2
        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true, assertedAtBusCycle);
        var entry = cpu.ProcessPendingInterrupts(mem);

        Assert.Equal(takenNow ? CPU.InterruptEntryCycles : 0UL, entry);
        Assert.Equal(takenNow ? IrqHandler : (ushort)(Start + 1), cpu.PC);

        if (!takenNow)
        {
            var result = cpu.ExecuteOneInstructionMinimal(mem);      // the second NOP, then the IRQ
            Assert.Equal(2 + CPU.InterruptEntryCycles, result.CyclesConsumed);
            Assert.Equal(IrqHandler, cpu.PC);
        }
    }

    [Fact]
    public void IRQ_asserted_without_a_cycle_is_taken_at_the_next_boundary()
    {
        var (cpu, mem) = NewCpu(0xEA);
        cpu.ExecuteOneInstructionMinimal(mem);

        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);

        Assert.Equal(CPU.InterruptEntryCycles, cpu.ProcessPendingInterrupts(mem));
        Assert.Equal(IrqHandler, cpu.PC);
    }

    [Theory]
    [InlineData(2, false)]   // asserted during cycle 2 of a 2-cycle NOP: deferred
    [InlineData(1, true)]
    public void NMI_edge_follows_the_same_sampling_rule(ulong pendingAtBusCycle, bool takenNow)
    {
        var (cpu, mem) = NewCpu(0xEA, 0xEA);
        cpu.ExecuteOneInstructionMinimal(mem);

        cpu.CPUInterrupts.SetNMISourceActive("device", pendingAtBusCycle);
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal(takenNow ? NmiHandler : (ushort)(Start + 1), cpu.PC);
        if (!takenNow)
        {
            cpu.ExecuteOneInstructionMinimal(mem);
            Assert.Equal(NmiHandler, cpu.PC);
        }
    }

    [Fact]
    public void The_assertion_cycle_is_that_of_the_first_source_to_pull_the_line_low()
    {
        var (cpu, mem) = NewCpu(0xEA);
        cpu.ExecuteOneInstructionMinimal(mem);                      // bus cycles 1-2

        cpu.CPUInterrupts.SetIRQSourceActive("early", autoAcknowledge: false, 1);
        cpu.CPUInterrupts.SetIRQSourceActive("late", autoAcknowledge: false, 2);

        Assert.Equal(1UL, cpu.CPUInterrupts.IRQAssertedAtBusCycle);
        Assert.Equal(CPU.InterruptEntryCycles, cpu.ProcessPendingInterrupts(mem));
    }

    [Theory]
    [InlineData(1, true)]    // taken branch, no page crossing (3 cycles): polls at the end of its first cycle
    [InlineData(2, false)]   // asserted during its second cycle: not seen until the next instruction
    public void Taken_branch_without_page_crossing_polls_only_at_the_end_of_its_first_cycle(ulong assertedAtBusCycle, bool takenNow)
    {
        var (cpu, mem) = NewCpu(0xD0, 0x01, 0xEA, 0xEA);   // BNE +1 ; NOP ; NOP
        cpu.ProcessorStatus.Zero = false;

        var branch = cpu.ExecuteOneInstructionMinimal(mem);          // bus cycles 1-3
        Assert.Equal(3UL, branch.CyclesConsumed);
        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true, assertedAtBusCycle);
        cpu.ProcessPendingInterrupts(mem);

        Assert.Equal(takenNow ? IrqHandler : (ushort)(Start + 3), cpu.PC);
    }

    [Fact]
    public void Taken_branch_with_page_crossing_polls_at_its_second_to_last_cycle()
    {
        // BNE at $10FD with offset +1 lands on $1100: taken, page crossed, 4 cycles.
        var cpu = new CPU();
        var mem = new Memory();
        mem[0x10FD] = 0xD0; mem[0x10FE] = 0x01; mem[0x1100] = 0xEA;
        mem.WriteWord(CPU.BrkIRQHandlerVector, IrqHandler);
        cpu.PC = 0x10FD; cpu.SP = 0xFF;
        cpu.ProcessorStatus.InterruptDisable = false;
        cpu.ProcessorStatus.Zero = false;

        var branch = cpu.ExecuteOneInstructionMinimal(mem);          // bus cycles 1-4
        Assert.Equal(4UL, branch.CyclesConsumed);
        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true, 3);

        Assert.Equal(CPU.InterruptEntryCycles, cpu.ProcessPendingInterrupts(mem));
        Assert.Equal(IrqHandler, cpu.PC);
    }

    [Fact]
    public void CLI_takes_effect_one_instruction_late()
    {
        var (cpu, mem) = NewCpu(0x58, 0xEA, 0xEA);   // CLI ; NOP ; NOP
        cpu.ProcessorStatus.InterruptDisable = true;
        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);

        var cli = cpu.ExecuteOneInstructionMinimal(mem);
        Assert.False(cpu.ProcessorStatus.InterruptDisable);
        Assert.Equal(2UL, cli.CyclesConsumed);              // the poll saw I still set
        Assert.Equal(Start + 1, cpu.PC);

        var nop = cpu.ExecuteOneInstructionMinimal(mem);
        Assert.Equal(2 + CPU.InterruptEntryCycles, nop.CyclesConsumed);
        Assert.Equal(IrqHandler, cpu.PC);
    }

    [Fact]
    public void SEI_still_lets_a_pending_IRQ_through_after_it()
    {
        var (cpu, mem) = NewCpu(0x78, 0xEA);   // SEI ; NOP
        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);

        var sei = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(2 + CPU.InterruptEntryCycles, sei.CyclesConsumed);   // the poll saw I still clear
        Assert.Equal(IrqHandler, cpu.PC);
        Assert.True(cpu.ProcessorStatus.InterruptDisable);
    }

    [Fact]
    public void PLP_clearing_I_takes_effect_one_instruction_late()
    {
        var (cpu, mem) = NewCpu(0x28, 0xEA, 0xEA);   // PLP ; NOP ; NOP
        cpu.ProcessorStatus.InterruptDisable = true;
        mem[0x01FF] = 0x00;                            // status with I clear
        cpu.SP = 0xFE;
        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);

        var plp = cpu.ExecuteOneInstructionMinimal(mem);
        Assert.False(cpu.ProcessorStatus.InterruptDisable);
        Assert.Equal(4UL, plp.CyclesConsumed);
        Assert.Equal(Start + 1, cpu.PC);

        var nop = cpu.ExecuteOneInstructionMinimal(mem);
        Assert.Equal(2 + CPU.InterruptEntryCycles, nop.CyclesConsumed);
    }

    [Fact]
    public void RTI_clearing_I_takes_effect_immediately()
    {
        var (cpu, mem) = NewCpu(0x40, 0xEA);   // RTI
        cpu.ProcessorStatus.InterruptDisable = true;
        mem[0x01FD] = 0x00;                     // status with I clear
        mem.WriteWord(0x01FE, 0x3000);          // return address
        cpu.SP = 0xFC;
        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: true);

        var rti = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(6 + CPU.InterruptEntryCycles, rti.CyclesConsumed);
        Assert.Equal(IrqHandler, cpu.PC);
    }
}
