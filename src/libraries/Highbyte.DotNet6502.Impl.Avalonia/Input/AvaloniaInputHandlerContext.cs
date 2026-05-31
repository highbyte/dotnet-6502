using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Input;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Impl.Avalonia.Input;

public class AvaloniaInputHandlerContext : IInputHandlerContext, IHostInputState
{
    public bool IsInitialized { get; private set; } = false;

    // Keyboard state tracking.
    // Tracked by PhysicalKey (W3C `code` — the key's physical position), not the layout-dependent
    // Key, so the neutral HostKey produced downstream is a true physical key as HostKey requires.
    public HashSet<PhysicalKey> KeysDown = new();
    private readonly Dictionary<PhysicalKey, HostKey> _hostKeyOverrides = new();
    private bool _capsLockOn;

    // Gamepad state tracking
    private readonly IGamepad _gamepad;

    /// <summary>
    /// Gets the gamepad provider.
    /// </summary>
    public IGamepad Gamepad => _gamepad;

    /// <summary>
    /// Gets the set of currently pressed gamepad buttons.
    /// Convenience property that delegates to the gamepad provider.
    /// </summary>
    public HashSet<GamepadButton> GamepadButtonsDown => _gamepad.ButtonsDown;

    /// <summary>
    /// Creates a new AvaloniaInputHandlerContext with a null gamepad (no gamepad support).
    /// </summary>
    public AvaloniaInputHandlerContext() : this(new NullGamepad())
    {
    }

    /// <summary>
    /// Creates a new AvaloniaInputHandlerContext with the specified gamepad provider.
    /// </summary>
    /// <param name="gamepad">The gamepad provider to use. Pass null to use a NullAvaloniaGamepad.</param>
    public AvaloniaInputHandlerContext(IGamepad? gamepad)
    {
        _gamepad = gamepad ?? new NullGamepad();
    }

    /// <summary>
    /// Browser-detected input environment, set once by a browser Avalonia head at startup before
    /// the input context is created. A native (desktop) head leaves these unset, and the
    /// <see cref="IHostInputState"/> members fall back to querying the OS directly.
    /// <para>
    /// Statics are used because the context is constructed deep inside shared Avalonia.Core code,
    /// out of reach of the browser head's startup; the browser environment is process-global, so
    /// a set-once static is equivalent to the constructor injection other browser hosts use.
    /// </para>
    /// </summary>
    public static string? BrowserDetectedKeyboardLayoutId { get; set; }

    /// <inheritdoc cref="BrowserDetectedKeyboardLayoutId"/>
    public static bool BrowserIsRunningOnMacOS { get; set; }

    public bool GetCapsLockState() => _capsLockOn;

    public void SetCapsLockState(bool capsLockOn) => _capsLockOn = capsLockOn;

    public void AddKeyDown(PhysicalKey key)
    {
        KeysDown.Add(key);
        _hostKeyOverrides.Remove(key);
        if (key == PhysicalKey.CapsLock)
            _capsLockOn = !_capsLockOn;
    }

    public void AddKeyDown(Key key, PhysicalKey physicalKey)
    {
        KeysDown.Add(physicalKey);
        if (TryMapLogicalOverride(key, out var hostKey))
            _hostKeyOverrides[physicalKey] = hostKey;
        else
            _hostKeyOverrides.Remove(physicalKey);

        if (physicalKey == PhysicalKey.CapsLock)
            _capsLockOn = !_capsLockOn;
    }

    public void RemoveKeyDown(PhysicalKey key)
    {
        KeysDown.Remove(key);
        _hostKeyOverrides.Remove(key);
    }

    public void RemoveKeyDown(Key key, PhysicalKey physicalKey)
    {
        KeysDown.Remove(physicalKey);
        _hostKeyOverrides.Remove(physicalKey);
    }

    public void ClearKeysDown()
    {
        KeysDown.Clear();
    }

    /// <summary>
    /// Updates gamepad state. Should be called once per frame.
    /// </summary>
    public void UpdateGamepad()
    {
        _gamepad.Update();
    }

    public void Init()
    {
        IsInitialized = true;
        KeysDown.Clear();
        _capsLockOn = false;
        _gamepad.Init();
    }

    public void Cleanup()
    {
        IsInitialized = false;
        KeysDown.Clear();
        _gamepad.Cleanup();
    }

    // ----------------------------------------------------------------------
    // IHostInputState: neutral input surface consumed by system input handlers.
    // ----------------------------------------------------------------------

    IReadOnlySet<HostKey> IHostInputState.KeysDown
    {
        get
        {
            var result = new HashSet<HostKey>();
            foreach (var key in KeysDown)
            {
                var hostKey = _hostKeyOverrides.TryGetValue(key, out var overrideKey)
                    ? overrideKey
                    : MapToHostKey(key);
                if (hostKey != HostKey.None)
                    result.Add(hostKey);
            }
            return result;
        }
    }

    IReadOnlySet<GamepadButton> IHostInputState.GamepadButtonsDown => GamepadButtonsDown;

    bool IHostInputState.CapsLockOn => GetCapsLockState();

    void IHostInputState.UpdatePerFrame() => UpdateGamepad();

    // Overridden so a browser Avalonia head's detected values win: the .NET runtime reports
    // OSPlatform.Browser in WASM, so the IHostInputState defaults can neither see the real OS nor
    // query the OS keyboard layout. A native head leaves the statics unset and these fall back
    // to the OS.
    bool IHostInputState.IsRunningOnMacOS =>
        BrowserIsRunningOnMacOS || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    string? IHostInputState.DetectNativeKeyboardLayoutId() =>
        BrowserDetectedKeyboardLayoutId ?? KeyboardLayoutDetector.DetectNativeLayoutId();

    private static bool TryMapLogicalOverride(Key key, out HostKey hostKey)
    {
        hostKey = key switch
        {
            Key.Insert => HostKey.Insert,
            Key.Delete => HostKey.Delete,
            Key.Home => HostKey.Home,
            Key.End => HostKey.End,
            Key.PageUp => HostKey.PageUp,
            Key.PageDown => HostKey.PageDown,
            _ => HostKey.None,
        };
        return hostKey != HostKey.None;
    }

    private static HostKey MapToHostKey(PhysicalKey key) => key switch
    {
        PhysicalKey.A => HostKey.KeyA, PhysicalKey.B => HostKey.KeyB, PhysicalKey.C => HostKey.KeyC,
        PhysicalKey.D => HostKey.KeyD, PhysicalKey.E => HostKey.KeyE, PhysicalKey.F => HostKey.KeyF,
        PhysicalKey.G => HostKey.KeyG, PhysicalKey.H => HostKey.KeyH, PhysicalKey.I => HostKey.KeyI,
        PhysicalKey.J => HostKey.KeyJ, PhysicalKey.K => HostKey.KeyK, PhysicalKey.L => HostKey.KeyL,
        PhysicalKey.M => HostKey.KeyM, PhysicalKey.N => HostKey.KeyN, PhysicalKey.O => HostKey.KeyO,
        PhysicalKey.P => HostKey.KeyP, PhysicalKey.Q => HostKey.KeyQ, PhysicalKey.R => HostKey.KeyR,
        PhysicalKey.S => HostKey.KeyS, PhysicalKey.T => HostKey.KeyT, PhysicalKey.U => HostKey.KeyU,
        PhysicalKey.V => HostKey.KeyV, PhysicalKey.W => HostKey.KeyW, PhysicalKey.X => HostKey.KeyX,
        PhysicalKey.Y => HostKey.KeyY, PhysicalKey.Z => HostKey.KeyZ,
        PhysicalKey.Digit0 => HostKey.Digit0, PhysicalKey.Digit1 => HostKey.Digit1,
        PhysicalKey.Digit2 => HostKey.Digit2, PhysicalKey.Digit3 => HostKey.Digit3,
        PhysicalKey.Digit4 => HostKey.Digit4, PhysicalKey.Digit5 => HostKey.Digit5,
        PhysicalKey.Digit6 => HostKey.Digit6, PhysicalKey.Digit7 => HostKey.Digit7,
        PhysicalKey.Digit8 => HostKey.Digit8, PhysicalKey.Digit9 => HostKey.Digit9,
        PhysicalKey.Space => HostKey.Space, PhysicalKey.Enter => HostKey.Enter,
        PhysicalKey.Tab => HostKey.Tab, PhysicalKey.Backspace => HostKey.Backspace,
        PhysicalKey.Escape => HostKey.Escape,
        PhysicalKey.F1 => HostKey.F1, PhysicalKey.F2 => HostKey.F2, PhysicalKey.F3 => HostKey.F3,
        PhysicalKey.F4 => HostKey.F4, PhysicalKey.F5 => HostKey.F5, PhysicalKey.F6 => HostKey.F6,
        PhysicalKey.F7 => HostKey.F7, PhysicalKey.F8 => HostKey.F8, PhysicalKey.F9 => HostKey.F9,
        PhysicalKey.F10 => HostKey.F10, PhysicalKey.F11 => HostKey.F11, PhysicalKey.F12 => HostKey.F12,
        PhysicalKey.Insert => HostKey.Insert, PhysicalKey.Delete => HostKey.Delete,
        PhysicalKey.Home => HostKey.Home, PhysicalKey.End => HostKey.End,
        PhysicalKey.PageUp => HostKey.PageUp, PhysicalKey.PageDown => HostKey.PageDown,
        PhysicalKey.ArrowUp => HostKey.ArrowUp, PhysicalKey.ArrowDown => HostKey.ArrowDown,
        PhysicalKey.ArrowLeft => HostKey.ArrowLeft, PhysicalKey.ArrowRight => HostKey.ArrowRight,
        PhysicalKey.ShiftLeft => HostKey.ShiftLeft, PhysicalKey.ShiftRight => HostKey.ShiftRight,
        PhysicalKey.ControlLeft => HostKey.ControlLeft, PhysicalKey.ControlRight => HostKey.ControlRight,
        PhysicalKey.AltLeft => HostKey.AltLeft, PhysicalKey.AltRight => HostKey.AltRight,
        PhysicalKey.MetaLeft => HostKey.MetaLeft, PhysicalKey.MetaRight => HostKey.MetaRight,
        PhysicalKey.CapsLock => HostKey.CapsLock,
        PhysicalKey.Backquote => HostKey.Backquote, PhysicalKey.Minus => HostKey.Minus,
        PhysicalKey.Equal => HostKey.Equal, PhysicalKey.BracketLeft => HostKey.BracketLeft,
        PhysicalKey.BracketRight => HostKey.BracketRight, PhysicalKey.Backslash => HostKey.Backslash,
        PhysicalKey.Semicolon => HostKey.Semicolon, PhysicalKey.Quote => HostKey.Quote,
        PhysicalKey.Comma => HostKey.Comma, PhysicalKey.Period => HostKey.Period,
        PhysicalKey.Slash => HostKey.Slash, PhysicalKey.IntlBackslash => HostKey.IntlBackslash,
        PhysicalKey.NumPad0 => HostKey.Numpad0, PhysicalKey.NumPad1 => HostKey.Numpad1,
        PhysicalKey.NumPad2 => HostKey.Numpad2, PhysicalKey.NumPad3 => HostKey.Numpad3,
        PhysicalKey.NumPad4 => HostKey.Numpad4, PhysicalKey.NumPad5 => HostKey.Numpad5,
        PhysicalKey.NumPad6 => HostKey.Numpad6, PhysicalKey.NumPad7 => HostKey.Numpad7,
        PhysicalKey.NumPad8 => HostKey.Numpad8, PhysicalKey.NumPad9 => HostKey.Numpad9,
        PhysicalKey.NumPadAdd => HostKey.NumpadAdd, PhysicalKey.NumPadSubtract => HostKey.NumpadSubtract,
        PhysicalKey.NumPadMultiply => HostKey.NumpadMultiply, PhysicalKey.NumPadDivide => HostKey.NumpadDivide,
        PhysicalKey.NumPadDecimal => HostKey.NumpadDecimal, PhysicalKey.NumPadEnter => HostKey.NumpadEnter,
        _ => HostKey.None,
    };
}
