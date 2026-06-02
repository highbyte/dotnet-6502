using System.Globalization;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.Input;

/// <summary>
/// The single, host-agnostic C64 input handler.
///
/// Replaces the formerly duplicated per-host handlers (<c>C64SilkNetInputHandler</c>,
/// <c>AvaloniaC64InputHandler</c>, <c>C64SadConsoleInputHandler</c>, <c>C64AspNetInputHandler</c>).
/// It reads input only through <see cref="IHostInputState"/> (neutral <see cref="HostKey"/> /
/// <see cref="GamepadButton"/>), maps it via <see cref="C64HostKeyboard"/> / <see cref="C64InputConfig"/>,
/// and applies the result to the C64 CIA1 keyboard/joystick. Each host now only translates its
/// native key/button types into the neutral abstractions.
/// </summary>
public class C64InputHandler : IInputConsumer
{
    private readonly C64 _c64;
    public ISystem System => _c64;

    private IHostInputState _inputState = default!;
    private readonly ILogger _logger;
    private readonly C64InputConfig _inputConfig;
    private readonly C64BasicCodingAssistant? _c64BasicCodingAssistant;

    private C64HostKeyboard _c64HostKeyboard = default!;
    private readonly List<C64Key> _c64KeysDownBuffer = new();
    private readonly List<HostKey[]> _foundHostKeyMappingsBuffer = new();
    private readonly HashSet<C64JoystickAction> _c64JoystickActionsBuffer = new();
    private readonly List<GamepadButton[]> _foundGamepadMappingsBuffer = new();
    private readonly HashSet<HostKey> _swappedHostKeysBuffer = new();
    private readonly HashSet<HostKey> _lastSwappedSourceKeysBuffer = new();

    /// <summary>
    /// True when the macOS ISO-keyboard <see cref="HostKey.Backquote"/> / <see cref="HostKey.IntlBackslash"/>
    /// swap must be applied — see <see cref="CaptureKeyboard"/>.
    /// </summary>
    private bool _swapBackquoteAndIntlBackslash;

    public Instrumentations Instrumentations { get; } = new();

    public bool CodingAssistantAvailable => _c64BasicCodingAssistant?.IsAvailable ?? false;

    private bool _codingAssistantEnabled;
    public bool CodingAssistantEnabled
    {
        get => _codingAssistantEnabled && CodingAssistantAvailable;
        set
        {
            if (!CodingAssistantAvailable && value)
                return;
            _codingAssistantEnabled = value;
        }
    }

    /// <param name="codingAssistant">Optional C64 BASIC coding assistant. Pass null on hosts that
    /// do not support it (e.g. SilkNet).</param>
    public C64InputHandler(
        C64 c64,
        ILoggerFactory loggerFactory,
        C64InputConfig inputConfig,
        C64BasicCodingAssistant? codingAssistant = null,
        bool codingAssistantDefaultEnabled = false)
    {
        _c64 = c64;
        _logger = loggerFactory.CreateLogger(nameof(C64InputHandler));
        _inputConfig = inputConfig;
        _c64BasicCodingAssistant = codingAssistant;
        _codingAssistantEnabled = codingAssistantDefaultEnabled;
    }

    public async Task CheckCodingAssistantAvailability()
    {
        if (_c64BasicCodingAssistant == null)
            return;
        await _c64BasicCodingAssistant.CheckAvailability();
        if (!_c64BasicCodingAssistant.IsAvailable)
        {
            _logger.LogError($"{_c64BasicCodingAssistant.LastError}");
            _logger.LogWarning("Coding assistant is not available. Disabling it.");
            _codingAssistantEnabled = false;
        }
    }

    public void Init(IHostInputState inputState)
    {
        _inputState = inputState;

        _c64HostKeyboard = new C64HostKeyboard(ResolveKeyboardLayout());

        // macOS reports the two ISO-keyboard keys §/< (the keys left of '1' and left of 'Z')
        // with hardware keycodes that are swapped relative to the W3C `code` convention that
        // HostKey follows: the § key arrives as IntlBackslash and the < key as Backquote. This
        // happens on macOS both natively and in a browser. IHostInputState.IsRunningOnMacOS
        // reports the real OS (a browser host detects it from the browser, since the .NET runtime
        // reports OSPlatform.Browser in WASM). The swap only makes sense on an ISO keyboard,
        // which a non-US C64 layout selection implies.
        _swapBackquoteAndIntlBackslash =
            _inputState.IsRunningOnMacOS
            && _c64HostKeyboard.Layout != C64KeyboardLayout.US;
        if (_swapBackquoteAndIntlBackslash)
            _logger.LogInformation("Applying macOS ISO-keyboard Backquote/IntlBackslash key swap.");
    }

    /// <summary>
    /// Resolves the host keyboard layout the C64 keyboard map is built for. Priority:
    /// <list type="number">
    /// <item>The explicit config setting <see cref="C64InputConfig.KeyboardLayout"/> — when set
    ///   (non-null), it forces that layout.</item>
    /// <item>Auto-detect: the host's native keyboard layout, via
    ///   <see cref="IHostInputState.DetectNativeKeyboardLayoutId"/> / <see cref="KeyboardLayoutResolver"/>.</item>
    /// <item>The OS/UI culture — inaccurate (it is not the physical keyboard) but better than
    ///   nothing.</item>
    /// <item>Default: <see cref="C64KeyboardLayout.US"/>.</item>
    /// </list>
    /// Steps 2–4 apply only when no explicit config setting is present (a <c>null</c>
    /// <see cref="C64InputConfig.KeyboardLayout"/>, i.e. an absent or empty config value).
    /// </summary>
    private C64KeyboardLayout ResolveKeyboardLayout()
    {
        if (_inputConfig.KeyboardLayout.HasValue)
        {
            _logger.LogInformation(
                $"C64 keyboard layout: {_inputConfig.KeyboardLayout.Value} (explicit config setting).");
            return _inputConfig.KeyboardLayout.Value;
        }

        var nativeLayoutId = _inputState.DetectNativeKeyboardLayoutId();
        var detected = KeyboardLayoutResolver.FromNativeLayoutId(nativeLayoutId);
        if (detected.HasValue)
        {
            _logger.LogInformation(
                $"C64 keyboard layout: {detected.Value} (auto-detected from host keyboard layout '{nativeLayoutId}').");
            return detected.Value;
        }

        var hostLayoutDesc = nativeLayoutId is null ? "not detectable" : $"'{nativeLayoutId}' unmapped";
        var culture = CultureInfo.CurrentCulture;
        var fromCulture = KeyboardLayoutResolver.FromCulture(culture);
        if (fromCulture.HasValue)
        {
            _logger.LogInformation(
                $"C64 keyboard layout: {fromCulture.Value} (from OS culture '{culture.Name}'; " +
                $"host keyboard layout {hostLayoutDesc}).");
            return fromCulture.Value;
        }

        _logger.LogInformation(
            $"C64 keyboard layout: {C64KeyboardLayout.US} (default; no config setting, " +
            $"host keyboard layout {hostLayoutDesc}, OS culture '{culture.Name}' unmapped).");
        return C64KeyboardLayout.US;
    }

    public void BeforeFrame()
    {
        _inputState.UpdatePerFrame();
        _c64.Cia1.Joystick.ClearJoystickActions();
        CaptureKeyboard();
        CaptureJoystick();
    }

    public void Cleanup()
    {
    }

    public List<string> GetDebugInfo() => new();

    private void CaptureKeyboard()
    {
        var hostKeysDown = _inputState.KeysDown;
        if (_swapBackquoteAndIntlBackslash)
            hostKeysDown = SwapBackquoteAndIntlBackslash(hostKeysDown);

        var c64KeysDown = GetC64KeysFromHostKeys(hostKeysDown, out bool restoreKeyPressed, out bool capsLockOn);

        LogKeyDiagnostics(hostKeysDown, c64KeysDown);

        if (CodingAssistantEnabled && c64KeysDown.Count > 0)
            _c64BasicCodingAssistant!.KeyWasPressed(c64KeysDown);

        _c64.InputInjector?.ApplyInjectedKeysTo(c64KeysDown);

        _c64.Cia1.Keyboard.SetKeysPressed(c64KeysDown, restoreKeyPressed, capsLockOn);
    }

    // Returns a copy of the held host keys with HostKey.Backquote and HostKey.IntlBackslash
    // exchanged — the macOS ISO-keyboard correction (see Init). Returns the input unchanged when
    // neither key is held, to avoid allocating on every frame.
    private IReadOnlySet<HostKey> SwapBackquoteAndIntlBackslash(IReadOnlySet<HostKey> hostKeysDown)
    {
        bool hasBackquote = hostKeysDown.Contains(HostKey.Backquote);
        bool hasIntlBackslash = hostKeysDown.Contains(HostKey.IntlBackslash);
        if (!hasBackquote && !hasIntlBackslash)
            return hostKeysDown;

        var swapped = _swappedHostKeysBuffer;
        if (hostKeysDown is HashSet<HostKey> hostKeyHashSet)
        {
            if (_lastSwappedSourceKeysBuffer.SetEquals(hostKeyHashSet))
                return swapped;

            _lastSwappedSourceKeysBuffer.Clear();
            _lastSwappedSourceKeysBuffer.UnionWith(hostKeyHashSet);
            swapped.Clear();
            swapped.UnionWith(hostKeyHashSet);
        }
        else
        {
            swapped.Clear();
            _lastSwappedSourceKeysBuffer.Clear();
            foreach (var key in hostKeysDown)
            {
                swapped.Add(key);
                _lastSwappedSourceKeysBuffer.Add(key);
            }
        }
        swapped.Remove(HostKey.Backquote);
        swapped.Remove(HostKey.IntlBackslash);
        if (hasBackquote)
            swapped.Add(HostKey.IntlBackslash);
        if (hasIntlBackslash)
            swapped.Add(HostKey.Backquote);
        return swapped;
    }

    // Logs the resolved HostKey -> C64Key mapping whenever the set of host keys held down changes.
    // Diagnostic aid for keyboard-layout issues: shows what neutral HostKey a host produced for a
    // physical key, and which C64 key(s) the active layout map resolved it to. Logged at Debug
    // level — enable Debug logging in the host to see it.
    private readonly HashSet<HostKey> _lastLoggedHostKeys = new();
    private void LogKeyDiagnostics(IReadOnlySet<HostKey> hostKeysDown, List<C64Key> c64KeysDown)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
            return;
        if (hostKeysDown.SetEquals(_lastLoggedHostKeys))
            return;
        _lastLoggedHostKeys.Clear();
        foreach (var k in hostKeysDown)
            _lastLoggedHostKeys.Add(k);

        if (hostKeysDown.Count == 0)
            return;
        _logger.LogDebug(
            $"Keyboard [{_c64HostKeyboard.Layout}]: HostKeys=[{string.Join(",", hostKeysDown)}] " +
            $"-> C64Keys=[{string.Join(",", c64KeysDown)}]");
    }

    private List<C64Key> GetC64KeysFromHostKeys(IReadOnlySet<HostKey> keysDown, out bool restoreKeyPressed, out bool capsLockOn)
    {
        // PageUp maps to the C64 Restore key, which is wired directly to NMI (not the keyboard matrix).
        restoreKeyPressed = keysDown.Contains(HostKey.PageUp);
        capsLockOn = _inputState.CapsLockOn;

        var c64KeysDown = _c64KeysDownBuffer;
        c64KeysDown.Clear();
        var foundMappings = _foundHostKeyMappingsBuffer;
        foundMappings.Clear();
        foreach (var mapKeys in _c64HostKeyboard.HostKeyToC64KeyMap.Keys)
        {
            if (MatchesAllKeys(keysDown, mapKeys))
            {
                // Remove any other mappings found that contains any of the keys in this mapping.
                for (int i = foundMappings.Count - 1; i >= 0; i--)
                {
                    var currentlyFoundMapKeys = foundMappings[i];
                    if (MappingsShareAnyKey(currentlyFoundMapKeys, mapKeys))
                        foundMappings.RemoveAt(i);
                }
                foundMappings.Add(mapKeys);
            }
        }

        foreach (var mapKeys in foundMappings)
        {
            var c64Keys = _c64HostKeyboard.HostKeyToC64KeyMap[mapKeys];
            foreach (var c64Key in c64Keys)
            {
                if (!c64KeysDown.Contains(c64Key))
                    c64KeysDown.Add(c64Key);
            }
        }
        return c64KeysDown;
    }

    private void CaptureJoystick()
    {
        var c64JoystickActions = GetC64JoystickActionsFromGamepad(_inputState.GamepadButtonsDown);
        _c64.Cia1.Joystick.SetJoystickActions(_inputConfig.CurrentJoystick, c64JoystickActions, overwrite: false);

        _c64.InputInjector?.ApplyInjectedJoystickActionsTo(_c64.Cia1.Joystick);
    }

    private HashSet<C64JoystickAction> GetC64JoystickActionsFromGamepad(IReadOnlySet<GamepadButton> gamepadButtonsDown)
    {
        var c64JoystickActions = _c64JoystickActionsBuffer;
        c64JoystickActions.Clear();
        var foundMappings = _foundGamepadMappingsBuffer;
        foundMappings.Clear();
        var map = _inputConfig.GamePadToC64JoystickMap[_inputConfig.CurrentJoystick];
        foreach (var mapKeys in map.Keys)
        {
            if (MatchesAllKeys(gamepadButtonsDown, mapKeys))
            {
                // Remove any other mappings found that contains any of the gamepad buttons in this mapping.
                for (int i = foundMappings.Count - 1; i >= 0; i--)
                {
                    var currentlyFoundMapKeys = foundMappings[i];
                    if (MappingsShareAnyKey(currentlyFoundMapKeys, mapKeys))
                        foundMappings.RemoveAt(i);
                }
                foundMappings.Add(mapKeys);
            }
        }

        foreach (var mapKeys in foundMappings)
        {
            var c64Actions = map[mapKeys];
            foreach (var c64Action in c64Actions)
                c64JoystickActions.Add(c64Action);
        }
        return c64JoystickActions;
    }

    private static bool MatchesAllKeys<T>(IReadOnlySet<T> pressedKeys, T[] mapKeys)
    {
        foreach (var mapKey in mapKeys)
        {
            if (!pressedKeys.Contains(mapKey))
                return false;
        }

        return true;
    }

    private static bool MappingsShareAnyKey<T>(T[] left, T[] right)
    {
        var comparer = EqualityComparer<T>.Default;
        foreach (var leftKey in left)
        {
            foreach (var rightKey in right)
            {
                if (comparer.Equals(leftKey, rightKey))
                    return true;
            }
        }

        return false;
    }
}
