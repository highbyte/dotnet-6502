using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// A system's per-frame input consumer, exposed via <see cref="ISystem.InputConsumer"/>.
///
/// Input mirrors the render/audio pipeline, but reversed: render and audio are outputs the system
/// produces, whereas input is produced by the host and consumed by the system. The host exposes
/// its keyboard/gamepad state through the neutral <see cref="IHostInputState"/>; the system
/// exposes a reusable <see cref="IInputConsumer"/> that reads that state each frame and applies it
/// to the emulated machine.
///
/// The host input state is supplied at <see cref="Init"/> time (not via the constructor) so the
/// consumer can be created before any host is bound — the counterpart of how a system's audio
/// provider is created before a host audio target exists.
/// </summary>
public interface IInputConsumer
{
    ISystem System { get; }

    /// <summary>
    /// Binds the host input state and performs one-time setup. Called once by the host app after
    /// the system is built and before the first frame.
    /// </summary>
    void Init(IHostInputState hostInputState);

    /// <summary>Called once per emulator frame, before the frame runs, to apply host input.</summary>
    void BeforeFrame();

    void Cleanup();

    List<string> GetDebugInfo();
    Instrumentations Instrumentations { get; }
}

public class NullInputConsumer : IInputConsumer
{
    private readonly ISystem _system;
    public ISystem System => _system;

    public NullInputConsumer(ISystem system)
    {
        _system = system;
    }
    public void Init(IHostInputState hostInputState)
    {
    }
    public void BeforeFrame()
    {
    }
    public void Cleanup()
    {
    }

    public List<string> GetDebugInfo() => new();

    public Instrumentations Instrumentations { get; } = new();
}
