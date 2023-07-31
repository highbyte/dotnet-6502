namespace Highbyte.DotNet6502.Systems;

public interface ISystem
{
    string Name { get; }
    string SystemInfo { get; }

    CPU CPU { get; }
    Memory Mem { get; }
    IScreen Screen { get; }

    public ExecEvaluatorTriggerResult ExecuteOneFrame(
        SystemRunner systemRunner,
        Dictionary<string, double> detailedStats,
        IExecEvaluator? execEvaluator = null);

    public ExecEvaluatorTriggerResult ExecuteOneInstruction(
        SystemRunner systemRunner,
        out InstructionExecResult instructionExecResult,
        Dictionary<string, double> detailedStats,
        IExecEvaluator? execEvaluator = null);
}
