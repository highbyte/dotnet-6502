namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Internal IExecEvaluator implementation for checking breakpoints
/// </summary>
internal class BreakpointEvaluator : IExecEvaluator
{
    private readonly DebugAdapterLogic _logic;

    public BreakpointEvaluator(DebugAdapterLogic logic)
    {
        _logic = logic;
    }

    public ExecEvaluatorTriggerResult Check(ExecState execState, CPU cpu, Memory mem)
    {
        // Check if we should break at current PC
        // Note: This is called synchronously, so we use GetAwaiter().GetResult()
        var shouldBreak = _logic.ShouldBreakAtCurrentPCAsync().GetAwaiter().GetResult();

        if (shouldBreak)
        {
            return ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.DebugBreakPoint, $"Breakpoint hit at ${cpu.PC:X4}");
        }

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    public ExecEvaluatorTriggerResult Check(InstructionExecResult lastInstructionExecResult, CPU cpu, Memory mem)
    {
        // This overload is called per-instruction in some systems (like C64)
        // Check if we should break at current PC
        var shouldBreak = _logic.ShouldBreakAtCurrentPCAsync().GetAwaiter().GetResult();

        if (shouldBreak)
        {
            return ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.DebugBreakPoint, $"Breakpoint hit at ${cpu.PC:X4}");
        }

        return ExecEvaluatorTriggerResult.NotTriggered;
    }
}
