using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Input;

/// <summary>Copies the host's physical keys into the Oric keyboard matrix once per frame.</summary>
public sealed class OricInputHandler : IInputConsumer
{
    private readonly OricMachine _oric;
    private IHostInputState _inputState = default!;

    public OricInputHandler(OricMachine oric) => _oric = oric;

    public ISystem System => _oric;
    public Instrumentations Instrumentations { get; } = new();

    public void Init(IHostInputState hostInputState) => _inputState = hostInputState;

    public void BeforeFrame()
    {
        _inputState.UpdatePerFrame();
        _oric.Keyboard.SetKeysPressed(_inputState.KeysDown);
    }

    public void Cleanup() => _oric.Keyboard.Reset();

    public List<string> GetDebugInfo() => [];
}
