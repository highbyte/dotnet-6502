namespace Highbyte.DotNet6502.Monitor;

public class BreakPointExecEvaluator : IExecEvaluator
{
    private readonly Dictionary<ushort, BreakPoint> _breakPoints;

    public BreakPointExecEvaluator(Dictionary<ushort, BreakPoint> breakPoints)
    {
        _breakPoints = breakPoints;
    }

    public ExecEvaluatorTriggerResult Check(ExecState execState, CPU cpu, Memory mem)
    {
        var pc = cpu.PC;
        if (_breakPoints.ContainsKey(pc) && _breakPoints[pc].Enabled)
            return ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.DebugBreakPoint);
        return ExecEvaluatorTriggerResult.NotTriggered;
    }
}
