namespace Highbyte.DotNet6502.Systems;

public interface ISystem
{
    string Name { get; }
    string SystemInfo { get; }

    CPU CPU { get; }
    Memory Mem { get; }

    public bool ExecuteOneFrame(IExecEvaluator? execEvaluator = null);
    public bool ExecuteOneInstruction();
}
