using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Input;
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

    /// <summary>
    /// The currently selected audio provider (audio counterpart of <see cref="RenderProvider"/>).
    /// Null if the system produces no audio.
    /// </summary>
    IAudioProvider? AudioProvider => null;

    /// <summary>
    /// Audio providers exposed by the system (audio counterpart of <see cref="RenderProviders"/>).
    /// Default is none; systems that produce audio override this.
    /// </summary>
    List<IAudioProvider> AudioProviders => new();

    /// <summary>
    /// The system's per-frame input consumer (input counterpart of <see cref="RenderProvider"/> /
    /// <see cref="AudioProvider"/>). Reads host input through the neutral
    /// <see cref="Input.IHostInputState"/> and applies it to the emulated machine.
    /// Null if the system consumes no input; systems that do override this.
    /// </summary>
    IInputConsumer? InputConsumer => null;

    IInputInjector? InputInjector => null;
}
