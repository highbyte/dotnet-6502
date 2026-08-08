using System.Globalization;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Input;

/// <summary>
/// Host-agnostic Apple II input handler.
///
/// The emulated machine has no keyboard matrix and no interrupt-driven scan: the encoder simply
/// latches the ASCII code of the most recent key press and raises a strobe that software polls.
/// This handler therefore converts <em>edges</em> — a key that was not held on the previous
/// frame — into a single latch write, plus a typematic auto-repeat while the key stays down so
/// holding a key behaves the way users expect from a modern keyboard.
/// </summary>
public class Apple2InputHandler : IInputConsumer
{
    /// <summary>Frames a key must be held before auto-repeat starts (~0.5s at 60 Hz).</summary>
    public const int AutoRepeatDelayFrames = 30;

    /// <summary>Frames between repeats once auto-repeat has started (~15 chars/s at 60 Hz).</summary>
    public const int AutoRepeatIntervalFrames = 4;

    private readonly Apple2System _apple2;
    private readonly ILogger _logger;
    private readonly Apple2InputConfig _inputConfig;

    // Reused per frame so a held stick does not allocate 60 times a second.
    private readonly HashSet<JoystickAction> _joystickActions = new();

    private IHostInputState _inputState = default!;

    private Apple2HostKeyboard _hostKeyboard = new(HostKeyboardLayout.US);
    private bool _swapBackquoteAndIntlBackslash;
    // Reused so the macOS ISO swap does not allocate a set every frame.
    private readonly HashSet<HostKey> _swappedHostKeysBuffer = new();

    private readonly HashSet<HostKey> _previousKeysDown = new();
    private HostKey _repeatingKey = HostKey.None;
    private int _framesHeld;

    public ISystem System => _apple2;
    public Instrumentations Instrumentations { get; } = new();

    public Apple2InputHandler(Apple2System apple2, ILoggerFactory loggerFactory, Apple2InputConfig? inputConfig = null)
    {
        _apple2 = apple2;
        _logger = loggerFactory.CreateLogger(nameof(Apple2InputHandler));
        _inputConfig = inputConfig ?? new Apple2InputConfig();
    }

    /// <summary>The gamepad and keyboard-joystick mapping in force.</summary>
    public Apple2InputConfig InputConfig => _inputConfig;

    /// <summary>The keyboard map in force, built for the resolved host keyboard layout.</summary>
    public Apple2HostKeyboard HostKeyboard => _hostKeyboard;

    public void Init(IHostInputState inputState)
    {
        _inputState = inputState;

        _hostKeyboard = new Apple2HostKeyboard(ResolveKeyboardLayout());

        // macOS reports the two ISO-keyboard keys §/< (the keys left of '1' and left of 'Z') with
        // hardware keycodes that are swapped relative to the W3C `code` convention that HostKey
        // follows: the § key arrives as IntlBackslash and the < key as Backquote. The swap only
        // makes sense on an ISO keyboard, which a non-US layout selection implies. Same correction
        // C64InputHandler applies.
        _swapBackquoteAndIntlBackslash =
            _inputState.IsRunningOnMacOS && _hostKeyboard.Layout != HostKeyboardLayout.US;
        if (_swapBackquoteAndIntlBackslash)
            _logger.LogInformation("Applying macOS ISO-keyboard Backquote/IntlBackslash key swap.");
    }

    /// <summary>
    /// Resolves the host keyboard layout the Apple II keyboard map is built for. Priority:
    /// <list type="number">
    /// <item>The explicit config setting <see cref="Apple2InputConfig.KeyboardLayout"/> — when set
    ///   (non-null), it forces that layout.</item>
    /// <item>Auto-detect: the host's native keyboard layout, via
    ///   <see cref="IHostInputState.DetectNativeKeyboardLayoutId"/> / <see cref="HostKeyboardLayoutResolver"/>.</item>
    /// <item>The OS/UI culture — inaccurate (it is not the physical keyboard) but better than
    ///   nothing.</item>
    /// <item>Default: <see cref="HostKeyboardLayout.US"/>.</item>
    /// </list>
    /// </summary>
    private HostKeyboardLayout ResolveKeyboardLayout()
    {
        if (_inputConfig.KeyboardLayout.HasValue)
        {
            _logger.LogInformation(
                $"Apple II keyboard layout: {_inputConfig.KeyboardLayout.Value} (explicit config setting).");
            return _inputConfig.KeyboardLayout.Value;
        }

        var nativeLayoutId = _inputState.DetectNativeKeyboardLayoutId();
        var detected = HostKeyboardLayoutResolver.FromNativeLayoutId(nativeLayoutId);
        if (detected.HasValue)
        {
            _logger.LogInformation(
                $"Apple II keyboard layout: {detected.Value} (auto-detected from host keyboard layout '{nativeLayoutId}').");
            return detected.Value;
        }

        var hostLayoutDesc = nativeLayoutId is null ? "not detectable" : $"'{nativeLayoutId}' unmapped";
        var culture = CultureInfo.CurrentCulture;
        var fromCulture = HostKeyboardLayoutResolver.FromCulture(culture);
        if (fromCulture.HasValue)
        {
            _logger.LogInformation(
                $"Apple II keyboard layout: {fromCulture.Value} (from OS culture '{culture.Name}'; " +
                $"host keyboard layout {hostLayoutDesc}).");
            return fromCulture.Value;
        }

        _logger.LogInformation(
            $"Apple II keyboard layout: {HostKeyboardLayout.US} (default; no config setting, " +
            $"host keyboard layout {hostLayoutDesc}, OS culture '{culture.Name}' unmapped).");
        return HostKeyboardLayout.US;
    }

    // Returns a copy of the held host keys with HostKey.Backquote and HostKey.IntlBackslash
    // exchanged — the macOS ISO-keyboard correction (see Init). Returns the input unchanged when
    // neither key is held, to avoid allocating on every frame.
    private IReadOnlySet<HostKey> SwapBackquoteAndIntlBackslash(IReadOnlySet<HostKey> hostKeysDown)
    {
        var hasBackquote = hostKeysDown.Contains(HostKey.Backquote);
        var hasIntlBackslash = hostKeysDown.Contains(HostKey.IntlBackslash);
        if (!hasBackquote && !hasIntlBackslash)
            return hostKeysDown;

        _swappedHostKeysBuffer.Clear();
        foreach (var key in hostKeysDown)
        {
            if (key == HostKey.Backquote)
                _swappedHostKeysBuffer.Add(HostKey.IntlBackslash);
            else if (key == HostKey.IntlBackslash)
                _swappedHostKeysBuffer.Add(HostKey.Backquote);
            else
                _swappedHostKeysBuffer.Add(key);
        }
        return _swappedHostKeysBuffer;
    }

    public void BeforeFrame()
    {
        _inputState.UpdatePerFrame();
        IReadOnlySet<HostKey> keysDown = _inputState.KeysDown;

        // Merge remotely injected keys so they flow through the same edge detection,
        // auto-repeat, and ASCII mapping as real keyboard input.
        if (_apple2.InputInjector.HasInjectedKeys)
        {
            var merged = new HashSet<HostKey>(keysDown);
            _apple2.InputInjector.ApplyInjectedKeysTo(merged);
            keysDown = merged;
        }

        if (_swapBackquoteAndIntlBackslash)
            keysDown = SwapBackquoteAndIntlBackslash(keysDown);

        keysDown = ApplyJoystick(keysDown);

        var shift = keysDown.Contains(HostKey.ShiftLeft) || keysDown.Contains(HostKey.ShiftRight);
        var control = keysDown.Contains(HostKey.ControlLeft) || keysDown.Contains(HostKey.ControlRight);
        var alt = keysDown.Contains(HostKey.AltLeft) || keysDown.Contains(HostKey.AltRight);

        // CTRL-RESET. The RESET key is wired to the 6502 /RES line (not the keyboard encoder),
        // with CTRL required on later II Plus keyboards. Mapped to Ctrl+F12, the same host combo
        // Virtual ][ uses. Edge-triggered so holding it does not reset every frame.
        if (control && keysDown.Contains(HostKey.F12) && !_previousKeysDown.Contains(HostKey.F12))
        {
            _apple2.Reset();
            _logger.LogDebug("Apple II input: CTRL-RESET (Ctrl+F12) — warm reset via the reset vector.");
        }

        var key = ResolveKeyToLatch(keysDown);
        if (key != HostKey.None && _hostKeyboard.TryGetAscii(key, shift, control, out var ascii, alt))
        {
            _apple2.Keyboard.KeyPressed(ascii);
            _logger.LogTrace("Apple II input: host={HostKey} ascii=${Ascii:X2}", key, ascii);
        }

        _previousKeysDown.Clear();
        foreach (var k in keysDown)
            _previousKeysDown.Add(k);
    }

    /// <summary>
    /// Turns the gamepad — and the host keyboard, when the keyboard joystick is enabled — into
    /// game-port state, and returns the keys that are still the keyboard's to handle.
    ///
    /// Keys claimed by the joystick are withheld from the keyboard: a key cannot both steer and
    /// type, and the arrow keys the joystick uses by default are real Apple II keys.
    /// </summary>
    private IReadOnlySet<HostKey> ApplyJoystick(IReadOnlySet<HostKey> keysDown)
    {
        _joystickActions.Clear();

        foreach (var (buttons, actions) in _inputConfig.GamePadToJoystickMap)
        {
            if (buttons.All(_inputState.GamepadButtonsDown.Contains))
            {
                foreach (var action in actions)
                    _joystickActions.Add(action);
            }
        }

        var remainingKeys = keysDown;
        if (_inputConfig.KeyboardJoystickEnabled)
        {
            HashSet<HostKey>? claimed = null;
            foreach (var (key, action) in _inputConfig.KeyboardToJoystickMap)
            {
                if (!keysDown.Contains(key))
                    continue;
                _joystickActions.Add(action);
                (claimed ??= new HashSet<HostKey>(keysDown)).Remove(key);
            }
            if (claimed != null)
                remainingKeys = claimed;
        }

        // Remotely injected actions join the same set, so a script drives the port through the
        // identical path a gamepad does.
        _apple2.InputInjector.ApplyInjectedJoystickActionsTo(_joystickActions);
        _apple2.InputInjector.ClearFrameInjectedJoystickActions();

        _apple2.GamePort.ApplyJoystickActions(_joystickActions);
        return remainingKeys;
    }

    public void Cleanup() { }

    public List<string> GetDebugInfo() => new()
    {
        $"Keyboard latch: ${_apple2.Keyboard.Latch:X2} (strobe {(_apple2.Keyboard.StrobeSet ? "set" : "clear")})",
    };

    /// <summary>
    /// Picks the key whose code should be latched this frame: a newly pressed key wins, and
    /// otherwise a still-held key repeats once its delay has elapsed.
    /// </summary>
    private HostKey ResolveKeyToLatch(IReadOnlySet<HostKey> keysDown)
    {
        var newlyPressed = HostKey.None;
        foreach (var key in keysDown)
        {
            if (Apple2HostKeyboard.ModifierKeys.Contains(key))
                continue;
            if (!_hostKeyboard.ProducesCharacter(key))
                continue;
            if (_previousKeysDown.Contains(key))
                continue;
            // Deterministic pick when several keys go down in the same frame.
            if (newlyPressed == HostKey.None || key < newlyPressed)
                newlyPressed = key;
        }

        if (newlyPressed != HostKey.None)
        {
            _repeatingKey = newlyPressed;
            _framesHeld = 0;
            return newlyPressed;
        }

        if (_repeatingKey == HostKey.None || !keysDown.Contains(_repeatingKey))
        {
            _repeatingKey = HostKey.None;
            _framesHeld = 0;
            return HostKey.None;
        }

        _framesHeld++;
        if (_framesHeld < AutoRepeatDelayFrames)
            return HostKey.None;

        return (_framesHeld - AutoRepeatDelayFrames) % AutoRepeatIntervalFrames == 0
            ? _repeatingKey
            : HostKey.None;
    }
}
