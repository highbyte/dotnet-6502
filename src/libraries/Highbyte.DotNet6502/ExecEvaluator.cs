using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502;

public interface IExecEvaluator
{
    public ExecEvaluatorTriggerResult Check(ExecState execState, CPU cpu, Memory mem);
    public ExecEvaluatorTriggerResult Check(InstructionExecResult lastInstructionExecResult, CPU cpu, Memory mem);
}

public class LegacyExecEvaluator : IExecEvaluator
{
    public ExecOptions ExecOptions => _execOptions;

    private readonly ExecOptions _execOptions;

    public static LegacyExecEvaluator OneInstructionExecEvaluator = new LegacyExecEvaluator(new ExecOptions { MaxNumberOfInstructions = 1 });

    public static LegacyExecEvaluator InstructionCountExecEvaluator(ulong numberOfInstructions)
    {
        return new LegacyExecEvaluator(new ExecOptions { MaxNumberOfInstructions = numberOfInstructions });
    }

    public LegacyExecEvaluator(ExecOptions execOptions)
    {
        _execOptions = execOptions;
    }

    public ExecEvaluatorTriggerResult Check(ExecState execState, CPU cpu, Memory mem)
    {
        ExecEvaluatorTriggerReasonType? execEvaluatorTriggerReasonType = null;
        string? triggerDescription = null;

        var instructionExecutionResult = execState.LastInstructionExecResult;

        // Check if we're configured to throw exception when unknown exception occurs
        if (instructionExecutionResult.UnknownInstruction && ExecOptions.UnknownInstructionThrowsException)
            throw new DotNet6502Exception($"Unknown opcode: {instructionExecutionResult.OpCodeByte.ToHex()}");

        if (instructionExecutionResult.IsBRKInstruction && ExecOptions.BRKInstructionStopsExecution)
        {
            execEvaluatorTriggerReasonType = ExecEvaluatorTriggerReasonType.Other;
            triggerDescription = "BRK instruction was configured to stop execition";
        }

        // Check if we should continue executing instructions
        if (ExecOptions.CyclesRequested.HasValue && execState.CyclesConsumed >= ExecOptions.CyclesRequested.Value)
        {
            execEvaluatorTriggerReasonType = ExecEvaluatorTriggerReasonType.Other;
            triggerDescription = "CyclesRequested";
        }
        if (ExecOptions.MaxNumberOfInstructions.HasValue && execState.InstructionsExecutionCount >= ExecOptions.MaxNumberOfInstructions.Value)
        {
            execEvaluatorTriggerReasonType = ExecEvaluatorTriggerReasonType.Other;
            triggerDescription = "MaxNumberOfInstruction";
        }
        if (!instructionExecutionResult.UnknownInstruction && ExecOptions.ExecuteUntilInstruction.HasValue && instructionExecutionResult.OpCodeByte == ExecOptions.ExecuteUntilInstruction.Value.ToByte())
        {
            execEvaluatorTriggerReasonType = ExecEvaluatorTriggerReasonType.Other;
            triggerDescription = "Specified instruction encountered";
        }
        if (ExecOptions.ExecuteUntilInstructions.Count > 0 && ExecOptions.ExecuteUntilInstructions.Contains(instructionExecutionResult.OpCodeByte))
        {
            execEvaluatorTriggerReasonType = ExecEvaluatorTriggerReasonType.Other;
            triggerDescription = "One of the specified instructions encountered";
        }
        if (ExecOptions.ExecuteUntilPC.HasValue && cpu.PC == ExecOptions.ExecuteUntilPC.Value)
        {
            execEvaluatorTriggerReasonType = ExecEvaluatorTriggerReasonType.Other;
            triggerDescription = "PC reached (after)";
        }
        if (ExecOptions.ExecuteUntilExecutedInstructionAtPC.HasValue && execState.PCBeforeLastOpCodeExecuted == ExecOptions.ExecuteUntilExecutedInstructionAtPC.Value)
        {
            execEvaluatorTriggerReasonType = ExecEvaluatorTriggerReasonType.Other;
            triggerDescription = "PC reached (before)";
        }

        if (!execEvaluatorTriggerReasonType.HasValue)
            return ExecEvaluatorTriggerResult.NotTriggered;
        else
            return ExecEvaluatorTriggerResult.CreateTrigger(execEvaluatorTriggerReasonType.Value, triggerDescription);
    }

    public ExecEvaluatorTriggerResult Check(InstructionExecResult lastInstructionExecResult, CPU cpu, Memory mem)
    {
        // Note: Method is not used, but is required by the interface.
        return ExecEvaluatorTriggerResult.NotTriggered;
    }
}

public class AlwaysExecEvaluator : IExecEvaluator
{
    public ExecEvaluatorTriggerResult Check(ExecState execState, CPU cpu, Memory mem) => ExecEvaluatorTriggerResult.NotTriggered;
    public ExecEvaluatorTriggerResult Check(InstructionExecResult lastInstructionExecResult, CPU cpu, Memory mem) => ExecEvaluatorTriggerResult.NotTriggered;
}
