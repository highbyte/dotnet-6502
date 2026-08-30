using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Input;

/// <summary>Copies the host's physical keys into the Oric keyboard matrix once per frame.</summary>
public sealed class OricInputHandler : IInputConsumer
{
    private readonly OricMachine _oric;
    private readonly OricInputConfig _inputConfig;
    private readonly HashSet<HostKey> _keyboardKeysBuffer = [];
    private readonly HashSet<JoystickAction> _keyboardJoystickActionsBuffer = [];
    private readonly HashSet<JoystickAction> _gamepadActionsBuffer = [];
    private IHostInputState _inputState = default!;

    public OricInputHandler(OricMachine oric, OricInputConfig? inputConfig = null)
    {
        _oric = oric;
        _inputConfig = inputConfig ?? new OricInputConfig();
    }

    public ISystem System => _oric;
    public Instrumentations Instrumentations { get; } = new();

    public void Init(IHostInputState hostInputState) => _inputState = hostInputState;

    public void BeforeFrame()
    {
        _inputState.UpdatePerFrame();
        _oric.Joystick.ClearJoystickActions();
        CaptureGamepad();
        CaptureKeyboard();
    }

    public void Cleanup()
    {
        _oric.Keyboard.Reset();
        _oric.Joystick.ClearJoystickActions();
    }

    public List<string> GetDebugInfo() => [];

    private void CaptureGamepad()
    {
        _gamepadActionsBuffer.Clear();
        foreach (var button in _inputState.GamepadButtonsDown)
        {
            if (_inputConfig.GamepadMap.TryGetValue(button, out var action))
                _gamepadActionsBuffer.Add(action);
        }
        _oric.Joystick.SetJoystickActions(_inputConfig.CurrentJoystick, _gamepadActionsBuffer, overwrite: false);
    }

    private void CaptureKeyboard()
    {
        if (!_oric.Joystick.KeyboardJoystickEnabled ||
            _oric.Joystick.Interface == OricJoystickInterface.None)
        {
            _oric.Keyboard.SetKeysPressed(_inputState.KeysDown);
            return;
        }

        _keyboardKeysBuffer.Clear();
        _keyboardKeysBuffer.UnionWith(_inputState.KeysDown);
        _keyboardJoystickActionsBuffer.Clear();
        foreach (var (hostKey, action) in _inputConfig.KeyboardJoystickMap)
        {
            if (_keyboardKeysBuffer.Remove(hostKey))
                _keyboardJoystickActionsBuffer.Add(action);
        }

        _oric.Joystick.SetJoystickActions(
            _oric.Joystick.KeyboardJoystick,
            _keyboardJoystickActionsBuffer,
            overwrite: false);
        _oric.Keyboard.SetKeysPressed(_keyboardKeysBuffer);
    }
}
