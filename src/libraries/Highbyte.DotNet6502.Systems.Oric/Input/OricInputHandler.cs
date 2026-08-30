using System.Globalization;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Input;

/// <summary>Translates host input into the Oric keyboard matrix and joystick once per frame.</summary>
public sealed class OricInputHandler : IInputConsumer
{
    private readonly OricMachine _oric;
    private readonly ILogger _logger;
    private readonly OricInputConfig _inputConfig;
    private readonly HashSet<HostKey> _hostKeysBuffer = [];
    private readonly HashSet<HostKey> _oricKeysBuffer = [];
    private readonly HashSet<JoystickAction> _keyboardJoystickActionsBuffer = [];
    private readonly HashSet<JoystickAction> _gamepadActionsBuffer = [];
    private IHostInputState _inputState = default!;

    private OricHostKeyboard _hostKeyboard = new(HostKeyboardLayout.US);
    private bool _swapBackquoteAndIntlBackslash;

    public OricInputHandler(OricMachine oric, OricInputConfig? inputConfig = null)
        : this(oric, NullLoggerFactory.Instance, inputConfig)
    {
    }

    public OricInputHandler(
        OricMachine oric,
        ILoggerFactory loggerFactory,
        OricInputConfig? inputConfig = null)
    {
        _oric = oric;
        _logger = loggerFactory.CreateLogger(nameof(OricInputHandler));
        _inputConfig = inputConfig ?? new OricInputConfig();
    }

    public ISystem System => _oric;
    public Instrumentations Instrumentations { get; } = new();

    public OricInputConfig InputConfig => _inputConfig;
    public OricHostKeyboard HostKeyboard => _hostKeyboard;

    public void Init(IHostInputState hostInputState)
    {
        _inputState = hostInputState;
        _hostKeyboard = new OricHostKeyboard(ResolveKeyboardLayout());
        _swapBackquoteAndIntlBackslash =
            _inputState.IsRunningOnMacOS && _hostKeyboard.Layout != HostKeyboardLayout.US;
        if (_swapBackquoteAndIntlBackslash)
            _logger.LogInformation("Applying macOS ISO-keyboard Backquote/IntlBackslash key swap.");
    }

    public void BeforeFrame()
    {
        _inputState.UpdatePerFrame();
        _oric.Joystick.ClearJoystickActions();
        CaptureGamepad();
        CaptureKeyboard();
        _oric.InputInjector.ApplyInjectedJoystickActionsTo(_oric.Joystick);
    }

    public void Cleanup()
    {
        _oric.Keyboard.Reset();
        _oric.Joystick.ClearJoystickActions();
        _oric.InputInjector.Clear();
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
        _hostKeysBuffer.Clear();
        _hostKeysBuffer.UnionWith(_inputState.KeysDown);
        _oric.InputInjector.ApplyInjectedKeysTo(_hostKeysBuffer);

        if (_swapBackquoteAndIntlBackslash)
            SwapBackquoteAndIntlBackslash();

        if (_oric.Joystick.KeyboardJoystickEnabled &&
            _oric.Joystick.Interface != OricJoystickInterface.None)
        {
            _keyboardJoystickActionsBuffer.Clear();
            foreach (var (hostKey, action) in _inputConfig.KeyboardJoystickMap)
            {
                if (_hostKeysBuffer.Remove(hostKey))
                    _keyboardJoystickActionsBuffer.Add(action);
            }

            _oric.Joystick.SetJoystickActions(
                _oric.Joystick.KeyboardJoystick,
                _keyboardJoystickActionsBuffer,
                overwrite: false);
        }

        _hostKeyboard.Translate(_hostKeysBuffer, _oricKeysBuffer);
        _oric.Keyboard.SetKeysPressed(_oricKeysBuffer);
    }

    private HostKeyboardLayout ResolveKeyboardLayout()
    {
        if (_inputConfig.KeyboardLayout.HasValue)
        {
            _logger.LogInformation(
                "Oric keyboard layout: {Layout} (explicit config setting).",
                _inputConfig.KeyboardLayout.Value);
            return _inputConfig.KeyboardLayout.Value;
        }

        var nativeLayoutId = _inputState.DetectNativeKeyboardLayoutId();
        var detected = HostKeyboardLayoutResolver.FromNativeLayoutId(nativeLayoutId);
        if (detected.HasValue)
        {
            _logger.LogInformation(
                "Oric keyboard layout: {Layout} (auto-detected from host keyboard layout '{NativeLayoutId}').",
                detected.Value,
                nativeLayoutId);
            return detected.Value;
        }

        var culture = CultureInfo.CurrentCulture;
        var fromCulture = HostKeyboardLayoutResolver.FromCulture(culture);
        if (fromCulture.HasValue)
        {
            _logger.LogInformation(
                "Oric keyboard layout: {Layout} (from OS culture '{Culture}').",
                fromCulture.Value,
                culture.Name);
            return fromCulture.Value;
        }

        _logger.LogInformation(
            "Oric keyboard layout: {Layout} (default; no host layout or culture mapping).",
            HostKeyboardLayout.US);
        return HostKeyboardLayout.US;
    }

    private void SwapBackquoteAndIntlBackslash()
    {
        var hasBackquote = _hostKeysBuffer.Remove(HostKey.Backquote);
        var hasIntlBackslash = _hostKeysBuffer.Remove(HostKey.IntlBackslash);
        if (hasBackquote)
            _hostKeysBuffer.Add(HostKey.IntlBackslash);
        if (hasIntlBackslash)
            _hostKeysBuffer.Add(HostKey.Backquote);
    }
}
