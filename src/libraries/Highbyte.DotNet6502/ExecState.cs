namespace Highbyte.DotNet6502;

public class ExecState
{
    public ulong CyclesConsumed { get; private set; }
    public ulong InstructionsExecutionCount { get; private set; }
    public ulong UnknownOpCodeCount { get; private set; }

    public InstructionExecResult LastInstructionExecResult { get; private set; }
    public bool LastOpCodeWasHandled { get { return !LastInstructionExecResult.UnknownInstruction; } }
    public ushort? PCBeforeLastOpCodeExecuted { get { return LastInstructionExecResult.AtPC; } }

    public ExecState()
    {
        CyclesConsumed = 0;
        InstructionsExecutionCount = 0;
        UnknownOpCodeCount = 0;
        LastInstructionExecResult = default;
    }

    public static ExecState ExecStateAfterInstruction(InstructionExecResult lastinstructionExecutionResult)
    {
        var execState = new ExecState();
        execState.InstructionsExecutionCount = 1;
        execState.CyclesConsumed = lastinstructionExecutionResult.CyclesConsumed;
        execState.LastInstructionExecResult = lastinstructionExecutionResult;
        execState.UnknownOpCodeCount = lastinstructionExecutionResult.UnknownInstruction ? (ulong)1 : (ulong)0;
        return execState;
    }

    public ExecState Clone()
    {
        return new ExecState
        {
            CyclesConsumed = this.CyclesConsumed,
            InstructionsExecutionCount = this.InstructionsExecutionCount,
            UnknownOpCodeCount = this.UnknownOpCodeCount,
            LastInstructionExecResult = this.LastInstructionExecResult,
        };
    }

    /// <summary>
    /// Overwrites the running totals with values captured earlier. Intended for snapshot restore,
    /// which has to make the cumulative cycle count continue from where the saved machine left off
    /// rather than from zero: peripherals that time themselves against it (the Apple II disk motor,
    /// paddle one-shots and speaker; the C64's SwiftLink receive pacing) hold <em>absolute</em>
    /// cycle stamps, which are only meaningful against a continuous counter.
    ///
    /// <para>This does not affect execution limits. <c>ExecOptions.CyclesRequested</c> and
    /// <c>MaxNumberOfInstructions</c> are evaluated against the per-invocation <see cref="ExecState"/>
    /// that <see cref="CPU.Execute"/> accumulates, not against these cumulative totals.</para>
    /// </summary>
    public void RestoreTotals(ulong cyclesConsumed, ulong instructionsExecutionCount, ulong unknownOpCodeCount)
    {
        CyclesConsumed = cyclesConsumed;
        InstructionsExecutionCount = instructionsExecutionCount;
        UnknownOpCodeCount = unknownOpCodeCount;
    }

    internal void UpdateTotal(ExecState newExecState)
    {
        CyclesConsumed += newExecState.CyclesConsumed;
        InstructionsExecutionCount += newExecState.InstructionsExecutionCount;
        UnknownOpCodeCount += newExecState.UnknownOpCodeCount;
        LastInstructionExecResult = newExecState.LastInstructionExecResult;
    }
    internal void UpdateTotal(InstructionExecResult instructionExecResult)
    {
        CyclesConsumed += instructionExecResult.CyclesConsumed;
        InstructionsExecutionCount += 1;
        if (instructionExecResult.UnknownInstruction)
            UnknownOpCodeCount += 1;
        LastInstructionExecResult = instructionExecResult;
    }
}
