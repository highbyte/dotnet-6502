using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Monitor;

public class BreakPointExecEvaluator : IExecEvaluator
{
    public bool StopAfterBRKInstruction { get; set; } = true;
    public bool StopAfterUnknownInstruction { get; set; } = true;

    private readonly Dictionary<ushort, BreakPoint> _breakPoints;

    public BreakPointExecEvaluator(Dictionary<ushort, BreakPoint> breakPoints)
    {
        _breakPoints = breakPoints;
    }

    public ExecEvaluatorTriggerResult Check(ExecState execState, CPU cpu, Memory mem)
    {
        return Check(execState.LastInstructionExecResult, cpu, mem);
    }

    public ExecEvaluatorTriggerResult Check(InstructionExecResult lastInstructionExecResult, CPU cpu, Memory mem)
    {
        // Check unknown instruction (PC will be at the next instruction)
        if (StopAfterUnknownInstruction && lastInstructionExecResult.UnknownInstruction)
            return ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.UnknownInstruction, $"Unknown instruction {lastInstructionExecResult.OpCodeByte.ToHex("", lowerCase: true)} at {lastInstructionExecResult.AtPC.ToHex("", lowerCase: true)} triggered stop.");

        // Check BRK instruction (PC will be at the next instruction)
        if (StopAfterBRKInstruction && lastInstructionExecResult.IsBRKInstruction)
            return ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.BRKInstruction, $"BRK instruction at {lastInstructionExecResult.AtPC.ToHex("", lowerCase: true)} triggered stop.");

        // Check breakpoints (will break before executing at the specified breakpoint address)
        var pc = cpu.PC;
        if (_breakPoints.ContainsKey(pc) && _breakPoints[pc].Enabled)
            return ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.DebugBreakPoint);
        return ExecEvaluatorTriggerResult.NotTriggered;

    }
}
