using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SadConsole;

public class SadConsoleInputHandlerContext : IInputHandlerContext, IHostInputState
{
    //private Keyboard _sadConsoleKeyboard;
    private Keyboard _sadConsoleKeyboard => GameHost.Instance.Keyboard;
    private readonly ILogger _logger;

    public bool IsInitialized { get; private set; }

    public List<Keys> KeysDown
    {
        get
        {
            var keysDown = _sadConsoleKeyboard.KeysDown;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                foreach (var key in keysDown)
                {
                    var hostKey = MapToHostKeyForCurrentLayout(key.Key);
                    var charDisplay = key.Character >= 32 && key.Character < 127
                        ? $"'{key.Character}'"
                        : $"0x{(int)key.Character:X2}";
                    _logger.LogDebug(
                        $"SadConsole key down: MonoGame Keys={key.Key}, Character={charDisplay}, -> HostKey={hostKey}");
                }
            }
            return keysDown.Select(x => x.Key).ToList();
        }
    }

    public SadConsoleInputHandlerContext(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(SadConsoleInputHandlerContext));
    }

    private bool? _isSwedishKeyboardLayout;

    /// <summary>
    /// Whether the host's active keyboard layout is Swedish. Used to apply a few SadConsole/MonoGame
    /// MonoGame-<see cref="Keys"/> → <see cref="HostKey"/> overrides where MonoGame's layout-bound
    /// <c>Keys</c> name diverges from the W3C physical-position naming that <see cref="HostKey"/>
    /// follows. Detected once via <see cref="KeyboardLayoutDetector"/> and cached.
    /// </summary>
    private bool IsSwedishKeyboardLayout
    {
        get
        {
            if (!_isSwedishKeyboardLayout.HasValue)
            {
                var nativeId = KeyboardLayoutDetector.DetectNativeLayoutId();
                _isSwedishKeyboardLayout =
                    !string.IsNullOrEmpty(nativeId)
                    && (nativeId.Contains("Swedish", StringComparison.OrdinalIgnoreCase)
                        || nativeId.Equals("0000041D", StringComparison.OrdinalIgnoreCase));
                if (_isSwedishKeyboardLayout.Value)
                    _logger.LogInformation(
                        $"SadConsole: applying Swedish-layout MonoGame Keys -> HostKey overrides (native layout id '{nativeId}').");
            }
            return _isSwedishKeyboardLayout.Value;
        }
    }

    /// <summary>
    /// SadConsole/MonoGame <see cref="Keys"/> values whose layout-bound meaning on a Swedish
    /// keyboard differs from the W3C-positional <see cref="HostKey"/> the shared C64 SV map
    /// expects. On Avalonia/SilkNet (which use W3C physical keys), the same physical key produces
    /// the override value below.
    /// </summary>
    private static readonly Dictionary<Keys, HostKey> s_swedishOverrides = new()
    {
        // Main "+" key right of "0" — Swedish keyboard at W3C `Minus` position.
        // (MonoGame reports it as Keys.Add — same enum as numeric keypad "+" — on macOS SDL.)
        [Keys.Add] = HostKey.Minus,
        // Main "-" key right of "." — Swedish keyboard at W3C `Slash` position.
        [Keys.OemMinus] = HostKey.Slash,
        // "'" key right of "Ä" — Swedish keyboard at W3C `Backslash` position.
        [Keys.OemQuotes] = HostKey.Backslash,
    };

    private HostKey MapToHostKeyForCurrentLayout(Keys key)
    {
        if (IsSwedishKeyboardLayout && s_swedishOverrides.TryGetValue(key, out var swedishHostKey))
            return swedishHostKey;
        return MapToHostKey(key);
    }

    //public void Init(Keyboard keyboard)
    public void Init()
    {
        //_sadConsoleKeyboard = keyboard;
        IsInitialized = true;
    }

    public void Cleanup()
    {
    }

    public bool GetCapsLockState()
    {
        // On Windows, Console.CapsLock can be used to check if CapsLock is on.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return System.Console.CapsLock;

        // On Linux and Mac: TODO: How to check this with SadConsole?
        return false;
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
                var hostKey = MapToHostKeyForCurrentLayout(key);
                if (hostKey != HostKey.None)
                    result.Add(hostKey);
            }
            return result;
        }
    }

    // SadConsole has no gamepad support.
    IReadOnlySet<GamepadButton> IHostInputState.GamepadButtonsDown { get; } = new HashSet<GamepadButton>();

    bool IHostInputState.CapsLockOn => GetCapsLockState();

    // SadConsole/MonoGame on macOS reports the ISO 102nd key (< / >, left of Z) directly as
    // Keys.OemBackslash -> HostKey.IntlBackslash, without the Backquote/IntlBackslash confusion
    // that Avalonia and SilkNet hit on macOS. The C64 input handler's macOS ISO key swap exists
    // only to undo that other-host confusion; applying it here would corrupt SadConsole's
    // already-correct HostKey and break the < and > keys on Swedish layouts. Suppress it.
    bool IHostInputState.IsRunningOnMacOS => false;

    void IHostInputState.UpdatePerFrame()
    {
        // KeysDown reads live from the SadConsole keyboard; nothing to poll.
    }

    private static HostKey MapToHostKey(Keys key) => key switch
    {
        Keys.A => HostKey.KeyA, Keys.B => HostKey.KeyB, Keys.C => HostKey.KeyC,
        Keys.D => HostKey.KeyD, Keys.E => HostKey.KeyE, Keys.F => HostKey.KeyF,
        Keys.G => HostKey.KeyG, Keys.H => HostKey.KeyH, Keys.I => HostKey.KeyI,
        Keys.J => HostKey.KeyJ, Keys.K => HostKey.KeyK, Keys.L => HostKey.KeyL,
        Keys.M => HostKey.KeyM, Keys.N => HostKey.KeyN, Keys.O => HostKey.KeyO,
        Keys.P => HostKey.KeyP, Keys.Q => HostKey.KeyQ, Keys.R => HostKey.KeyR,
        Keys.S => HostKey.KeyS, Keys.T => HostKey.KeyT, Keys.U => HostKey.KeyU,
        Keys.V => HostKey.KeyV, Keys.W => HostKey.KeyW, Keys.X => HostKey.KeyX,
        Keys.Y => HostKey.KeyY, Keys.Z => HostKey.KeyZ,
        Keys.D0 => HostKey.Digit0, Keys.D1 => HostKey.Digit1, Keys.D2 => HostKey.Digit2,
        Keys.D3 => HostKey.Digit3, Keys.D4 => HostKey.Digit4, Keys.D5 => HostKey.Digit5,
        Keys.D6 => HostKey.Digit6, Keys.D7 => HostKey.Digit7, Keys.D8 => HostKey.Digit8,
        Keys.D9 => HostKey.Digit9,
        Keys.Space => HostKey.Space, Keys.Enter => HostKey.Enter, Keys.Tab => HostKey.Tab,
        Keys.Back => HostKey.Backspace, Keys.Escape => HostKey.Escape,
        Keys.F1 => HostKey.F1, Keys.F2 => HostKey.F2, Keys.F3 => HostKey.F3,
        Keys.F4 => HostKey.F4, Keys.F5 => HostKey.F5, Keys.F6 => HostKey.F6,
        Keys.F7 => HostKey.F7, Keys.F8 => HostKey.F8, Keys.F9 => HostKey.F9,
        Keys.F10 => HostKey.F10, Keys.F11 => HostKey.F11, Keys.F12 => HostKey.F12,
        Keys.Insert => HostKey.Insert, Keys.Delete => HostKey.Delete,
        Keys.Home => HostKey.Home, Keys.End => HostKey.End,
        Keys.PageUp => HostKey.PageUp, Keys.PageDown => HostKey.PageDown,
        Keys.Up => HostKey.ArrowUp, Keys.Down => HostKey.ArrowDown,
        Keys.Left => HostKey.ArrowLeft, Keys.Right => HostKey.ArrowRight,
        Keys.LeftShift => HostKey.ShiftLeft, Keys.RightShift => HostKey.ShiftRight,
        Keys.LeftControl => HostKey.ControlLeft, Keys.RightControl => HostKey.ControlRight,
        Keys.LeftAlt => HostKey.AltLeft, Keys.RightAlt => HostKey.AltRight,
        Keys.LeftWindows => HostKey.MetaLeft, Keys.RightWindows => HostKey.MetaRight,
        Keys.CapsLock => HostKey.CapsLock,
        Keys.OemTilde => HostKey.Backquote, Keys.OemMinus => HostKey.Minus,
        Keys.OemPlus => HostKey.Equal, Keys.OemOpenBrackets => HostKey.BracketLeft,
        Keys.OemCloseBrackets => HostKey.BracketRight, Keys.OemPipe => HostKey.Backslash,
        Keys.OemSemicolon => HostKey.Semicolon, Keys.OemQuotes => HostKey.Quote,
        Keys.OemComma => HostKey.Comma, Keys.OemPeriod => HostKey.Period,
        Keys.OemQuestion => HostKey.Slash, Keys.OemBackslash => HostKey.IntlBackslash,
        Keys.Add => HostKey.NumpadAdd,
        _ => HostKey.None,
    };
}
