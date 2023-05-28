namespace Highbyte.DotNet6502.Systems;

public interface ISystem
{
    string Name { get; }
    string SystemInfo { get; }

    CPU CPU { get; }
    Memory Mem { get; }

    public bool ExecuteOneFrame(
        IExecEvaluator? execEvaluator = null,
        Action<ISystem, Dictionary<string, double>>? postInstructionCallback = null,
        Dictionary<string, double>? detailedStats = null);
    public bool ExecuteOneInstruction();
}
