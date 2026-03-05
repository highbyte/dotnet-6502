using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.Systems;

public interface ISystem
{
    string Name { get; }
    List<string> SystemInfo { get; }
    List<KeyValuePair<string, Func<string>>> DebugInfo { get; }

    CPU CPU { get; }
    Memory Mem { get; }
    IScreen Screen { get; }

    ExecEvaluatorTriggerResult ExecuteOneFrame(
        IExecEvaluator? execEvaluator = null);

    ExecEvaluatorTriggerResult ExecuteOneInstruction(
        out InstructionExecResult instructionExecResult,
        IExecEvaluator? execEvaluator = null);

    bool InstrumentationEnabled { get; set; }
    Instrumentations Instrumentations { get; }

    IRenderProvider? RenderProvider { get; }
    List<IRenderProvider> RenderProviders { get; }
}
