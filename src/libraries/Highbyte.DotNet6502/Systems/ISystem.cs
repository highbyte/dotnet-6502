using Highbyte.DotNet6502.Instrumentation;

namespace Highbyte.DotNet6502.Systems;

public interface ISystem
{
    string Name { get; }
    List<string> SystemInfo { get; }

    CPU CPU { get; }
    Memory Mem { get; }
    IScreen Screen { get; }

    ExecEvaluatorTriggerResult ExecuteOneFrame(
        SystemRunner systemRunner,
        IExecEvaluator? execEvaluator = null);

    ExecEvaluatorTriggerResult ExecuteOneInstruction(
        SystemRunner systemRunner,
        out InstructionExecResult instructionExecResult,
        IExecEvaluator? execEvaluator = null);

    bool InstrumentationEnabled { get; set; }
    Instrumentations Instrumentations { get; }
}
