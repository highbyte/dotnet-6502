using System.Runtime.CompilerServices;
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

    public readonly static LegacyExecEvaluator OneInstructionExecEvaluator = new LegacyExecEvaluator(new ExecOptions { MaxNumberOfInstructions = 1 });
    public readonly static LegacyExecEvaluator UntilBRKExecEvaluator = new LegacyExecEvaluator(new ExecOptions { BRKInstructionStopsExecution = true });

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
        var result = execState.LastInstructionExecResult;

        if (result.UnknownInstruction && ExecOptions.UnknownInstructionThrowsException)
            throw new DotNet6502Exception($"Unknown opcode: {result.OpCodeByte.ToHex()}");

        // Each helper checks an orthogonal group of stop conditions and returns the matching
        // description (or null). When multiple groups match simultaneously, the description
        // from the last group called wins — which mirrors the original's last-wins behavior
        // across an unordered set of independent conditions. Helpers are marked
        // AggressiveInlining so the JIT folds them back into Check on the hot per-instruction
        // path — the IL ends up equivalent to the original single-method form. The struct is
        // passed by `in` ref to avoid copies even before inlining.
        var triggerDescription = CheckInstructionStopConditions(in result);
        triggerDescription = CheckExecutionLimitConditions(execState) ?? triggerDescription;
        triggerDescription = CheckProgramCounterConditions(execState, cpu, in result) ?? triggerDescription;

        return triggerDescription is null
            ? ExecEvaluatorTriggerResult.NotTriggered
            : ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.Other, triggerDescription);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? CheckInstructionStopConditions(in InstructionExecResult result)
    {
        string? description = null;
        if (result.IsBRKInstruction && ExecOptions.BRKInstructionStopsExecution)
            description = "BRK instruction was configured to stop execition";
        if (result.IsValid && !result.UnknownInstruction && ExecOptions.ExecuteUntilInstruction.HasValue && result.OpCodeByte == ExecOptions.ExecuteUntilInstruction.Value.ToByte())
            description = "Specified instruction encountered";
        if (result.IsValid && ExecOptions.ExecuteUntilInstructions.Count > 0 && ExecOptions.ExecuteUntilInstructions.Contains(result.OpCodeByte))
            description = "One of the specified instructions encountered";
        return description;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? CheckExecutionLimitConditions(ExecState execState)
    {
        string? description = null;
        if (ExecOptions.CyclesRequested.HasValue && execState.CyclesConsumed >= ExecOptions.CyclesRequested.Value)
            description = "CyclesRequested";
        if (ExecOptions.MaxNumberOfInstructions.HasValue && execState.InstructionsExecutionCount >= ExecOptions.MaxNumberOfInstructions.Value)
            description = "MaxNumberOfInstruction";
        return description;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? CheckProgramCounterConditions(ExecState execState, CPU cpu, in InstructionExecResult result)
    {
        string? description = null;
        if (ExecOptions.ExecuteUntilPC.HasValue && cpu.PC == ExecOptions.ExecuteUntilPC.Value)
            description = "PC reached (after)";
        if (result.IsValid && ExecOptions.ExecuteUntilExecutedInstructionAtPC.HasValue && execState.PCBeforeLastOpCodeExecuted == ExecOptions.ExecuteUntilExecutedInstructionAtPC.Value)
            description = "PC reached (before)";
        return description;
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
