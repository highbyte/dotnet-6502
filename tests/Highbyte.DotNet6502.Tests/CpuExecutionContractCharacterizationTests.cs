using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// Characterizes the instruction-level execution contract of <see cref="CPU"/> as it is today:
/// event ordering, evaluator timing, unknown-opcode and JAM behavior, counters, and cloning.
///
/// These tests pin observable behavior that the cycle-steppable execution work must preserve
/// while it replaces the executor underneath the instruction API. A deliberate change to any of
/// them is a public behavior change and must update the test together with the documentation.
/// Interrupt-cycle attribution is characterized separately in <see cref="CPUInterruptBoundaryTests"/>,
/// ordered bus accesses in <see cref="CpuBusAccessCharacterizationTests"/>.
/// </summary>
public class CpuExecutionContractCharacterizationTests
{
    private const ushort ProgramStart = 0x1000;

    private static (CPU cpu, Memory mem) NewCpuWithNops(CpuCompatibilityProfile profile = CpuCompatibilityProfile.ExperimentalUnofficial)
    {
        var cpu = new CPU(profile);
        var mem = new Memory();
        for (ushort address = ProgramStart; address < ProgramStart + 0x100; address++)
            mem[address] = (byte)OpCodeId.NOP;
        cpu.PC = ProgramStart;
        return (cpu, mem);
    }

    private sealed class RecordingEvaluator : IExecEvaluator
    {
        public List<ushort> PcAtCheck { get; } = new();
        public int TriggerOnCheckNumber { get; init; } = int.MaxValue;

        public ExecEvaluatorTriggerResult Check(ExecState execState, CPU cpu, Memory mem)
        {
            PcAtCheck.Add(cpu.PC);
            return PcAtCheck.Count >= TriggerOnCheckNumber
                ? ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.Other, "test")
                : ExecEvaluatorTriggerResult.NotTriggered;
        }

        public ExecEvaluatorTriggerResult Check(InstructionExecResult lastInstructionExecResult, CPU cpu, Memory mem)
            => ExecEvaluatorTriggerResult.NotTriggered;
    }

    [Fact]
    public void Execute_Fires_ToBeExecuted_Then_Executed_Exactly_Once_Per_Instruction()
    {
        var (cpu, mem) = NewCpuWithNops();
        var log = new List<string>();
        cpu.InstructionToBeExecuted += (_, e) => log.Add($"before@{e.CPU.PC:X4}");
        cpu.InstructionExecuted += (_, e) => log.Add($"after@{e.CPU.PC:X4}:{e.InstructionExecState.InstructionsExecutionCount}");

        var execState = cpu.Execute(mem, new LegacyExecEvaluator(new ExecOptions { MaxNumberOfInstructions = 3 }));

        // The InstructionExecuted event carries the state of THAT instruction (count 1, its own
        // cycles), not the running totals; the totals are on the CPU and the returned ExecState.
        Assert.Equal(
            ["before@1000", "after@1001:1", "before@1001", "after@1002:1", "before@1002", "after@1003:1"],
            log);
        Assert.Equal(3ul, execState.InstructionsExecutionCount);
        Assert.Equal(3ul, cpu.ExecState.InstructionsExecutionCount);
    }

    [Fact]
    public void Minimal_Path_Fires_No_Instruction_Events()
    {
        var (cpu, mem) = NewCpuWithNops();
        var fired = 0;
        cpu.InstructionToBeExecuted += (_, _) => fired++;
        cpu.InstructionExecuted += (_, _) => fired++;
        cpu.UnknownOpCodeDetected += (_, _) => fired++;
        mem[ProgramStart + 1] = 0x02; // undefined below FullUnofficial

        cpu.ExecuteOneInstructionMinimal(mem);
        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0, fired);
        Assert.Equal(1ul, cpu.ExecState.UnknownOpCodeCount);
    }

    [Fact]
    public void Evaluators_Run_Before_Each_Instruction_And_A_Trigger_Prevents_Execution()
    {
        var (cpu, mem) = NewCpuWithNops();
        var evaluator = new RecordingEvaluator { TriggerOnCheckNumber = 3 };

        var execState = cpu.Execute(mem, evaluator);

        // Checked at $1000 (ran), $1001 (ran), $1002 (triggered, not run).
        Assert.Equal([0x1000, 0x1001, 0x1002], evaluator.PcAtCheck);
        Assert.Equal((ushort)0x1002, cpu.PC);
        Assert.Equal(2ul, execState.InstructionsExecutionCount);
        Assert.Equal(4ul, execState.CyclesConsumed);
    }

    [Fact]
    public void Unknown_OpCode_Costs_One_Cycle_Advances_PC_By_One_And_Is_Reported_Once()
    {
        var (cpu, mem) = NewCpuWithNops();
        mem[ProgramStart] = 0x02; // undefined below FullUnofficial
        var reported = new List<byte>();
        cpu.UnknownOpCodeDetected += (_, e) => reported.Add(e.OpCode);

        var execState = cpu.Execute(mem, new LegacyExecEvaluator(new ExecOptions
        {
            MaxNumberOfInstructions = 1,
            UnknownInstructionThrowsException = false,
        }));

        Assert.Equal([(byte)0x02], reported);
        Assert.Equal((ushort)(ProgramStart + 1), cpu.PC);
        Assert.Equal(1ul, execState.CyclesConsumed);
        Assert.Equal(1ul, execState.UnknownOpCodeCount);
        Assert.False(execState.LastOpCodeWasHandled);
        Assert.True(execState.LastInstructionExecResult.UnknownInstruction);
    }

    [Fact]
    public void Jam_Halts_The_Cpu_After_Two_Cycles_And_Halted_Steps_Cost_Nothing()
    {
        var (cpu, mem) = NewCpuWithNops(CpuCompatibilityProfile.FullUnofficial);
        mem[ProgramStart] = 0x02; // JAM

        var jam = cpu.ExecuteOneInstructionMinimal(mem);
        var afterJam = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.True(cpu.IsHalted);
        Assert.True(jam.HaltedCpu);
        Assert.Equal(2ul, jam.CyclesConsumed);
        Assert.Equal((ushort)(ProgramStart + 1), cpu.PC);

        Assert.Equal(0ul, afterJam.CyclesConsumed);
        Assert.False(afterJam.IsValid);
        Assert.Equal(2ul, cpu.ExecState.CyclesConsumed);
        Assert.Equal(1ul, cpu.ExecState.InstructionsExecutionCount);
    }

    [Fact]
    public void Reset_Clears_A_Jam_Halt_And_Starts_At_The_Reset_Vector()
    {
        var (cpu, mem) = NewCpuWithNops(CpuCompatibilityProfile.FullUnofficial);
        mem[ProgramStart] = 0x02; // JAM
        mem.WriteWord(CPU.ResetVector, 0x2000);
        cpu.ExecuteOneInstructionMinimal(mem);

        cpu.Reset(mem);

        Assert.False(cpu.IsHalted);
        Assert.Equal((ushort)0x2000, cpu.PC);
    }

    [Fact]
    public void Cumulative_Counters_Accumulate_Across_Both_Execution_Paths()
    {
        var (cpu, mem) = NewCpuWithNops();
        mem[ProgramStart + 2] = 0x02; // undefined below FullUnofficial

        cpu.ExecuteOneInstructionMinimal(mem);                              // NOP, 2 cycles
        cpu.Execute(mem, new LegacyExecEvaluator(new ExecOptions           // NOP + unknown
        {
            MaxNumberOfInstructions = 2,
            UnknownInstructionThrowsException = false,
        }));
        cpu.ExecuteOneInstruction(mem);                                     // NOP, 2 cycles

        Assert.Equal(4ul, cpu.ExecState.InstructionsExecutionCount);
        Assert.Equal(7ul, cpu.ExecState.CyclesConsumed);
        Assert.Equal(1ul, cpu.ExecState.UnknownOpCodeCount);
    }

    [Fact]
    public void Clone_Copies_Registers_Flags_Counters_Model_And_Profile()
    {
        var cpu = new CPU(CpuCompatibilityProfile.FullUnofficial)
        {
            PC = 0x1234, SP = 0xF0, A = 0x11, X = 0x22, Y = 0x33,
        };
        cpu.ProcessorStatus.Carry = true;
        cpu.ProcessorStatus.Decimal = true;
        var mem = new Memory();
        mem[0x1234] = (byte)OpCodeId.NOP;
        cpu.ExecuteOneInstructionMinimal(mem);

        var clone = cpu.Clone();

        Assert.Equal(cpu.PC, clone.PC);
        Assert.Equal(cpu.SP, clone.SP);
        Assert.Equal(cpu.A, clone.A);
        Assert.Equal(cpu.X, clone.X);
        Assert.Equal(cpu.Y, clone.Y);
        Assert.Equal(cpu.ProcessorStatus.Value, clone.ProcessorStatus.Value);
        Assert.Equal(cpu.ExecState.CyclesConsumed, clone.ExecState.CyclesConsumed);
        Assert.Equal(cpu.ExecState.InstructionsExecutionCount, clone.ExecState.InstructionsExecutionCount);
        Assert.Equal(cpu.CpuModelId, clone.CpuModelId);
        Assert.Equal(cpu.CompatibilityProfile, clone.CompatibilityProfile);
        Assert.NotSame(cpu.ExecState, clone.ExecState);
    }

    [Fact]
    public void Clone_Does_Not_Copy_Interrupt_Lines_Today()
    {
        // Current behavior, recorded on purpose: a clone starts with deasserted interrupt lines
        // and its own source registry. Whether a clone should carry pending interrupts is an
        // open question for the cycle work; changing it must update this test deliberately.
        var cpu = new CPU();
        cpu.CPUInterrupts.SetIRQSourceActive("device", autoAcknowledge: false);
        cpu.CPUInterrupts.SetNMISourceActive("nmi");

        var clone = cpu.Clone();

        Assert.NotSame(cpu.CPUInterrupts, clone.CPUInterrupts);
        Assert.False(clone.CPUInterrupts.IRQLineEnabled);
        Assert.False(clone.CPUInterrupts.NMIPending);
        Assert.Equal(0, clone.CPUInterrupts.RegisteredSourceCount);
    }
}
