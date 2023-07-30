namespace Highbyte.DotNet6502.Systems;

public interface ISystem
{
    string Name { get; }
    string SystemInfo { get; }

    CPU CPU { get; }
    Memory Mem { get; }
    IScreen Screen { get; }

    public ExecEvaluatorTriggerResult ExecuteOneFrame(
        IExecEvaluator? execEvaluator = null,
        Action<ISystem, Dictionary<string, double>>? postInstructionCallback = null,
        Dictionary<string, double>? detailedStats = null);
    public ExecEvaluatorTriggerResult ExecuteOneInstruction(
        IExecEvaluator? execEvaluator = null);
}
