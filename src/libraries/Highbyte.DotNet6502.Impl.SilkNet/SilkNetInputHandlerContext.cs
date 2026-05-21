using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging;
//using Silk.NET.SDL;

namespace Highbyte.DotNet6502.Impl.SilkNet;

public class SilkNetInputHandlerContext : IInputHandlerContext, IHostInputState
{
    private readonly IWindow _silkNetWindow;
    private readonly ILogger _logger;
    private static IInputContext s_inputcontext = default!;
    public IInputContext InputContext => s_inputcontext;

    // Keyboard
    private IKeyboard _primaryKeyboard = default!;
    public IKeyboard PrimaryKeyboard => _primaryKeyboard;
    public HashSet<Key> KeysDown = new();
    private bool _capsLockKeyDownCaptured;
    private bool _capsLockOn;

    // Gamepad
    private Silk.NET.Input.IGamepad? _currentGamePad;
    public Silk.NET.Input.IGamepad? CurrentGamePad => _currentGamePad;
    public HashSet<ButtonName> GamepadButtonsDown = new();

    public bool Quit { get; private set; }

    public bool IsKeyPressed(Key key) => _primaryKeyboard.IsKeyPressed(key);

    public bool IsInitialized { get; private set; }

    public SilkNetInputHandlerContext(IWindow silkNetWindow, ILoggerFactory loggerFactory)
    {
        _silkNetWindow = silkNetWindow;
        _logger = loggerFactory.CreateLogger(nameof(SilkNetInputHandlerContext));
    }

    public void Init()
    {
        Quit = false;

        s_inputcontext = _silkNetWindow.CreateInput();

        if (s_inputcontext == null)
            throw new DotNet6502Exception("Silk.NET Input Context not found.");

        s_inputcontext.ConnectionChanged += ConnectionChanged;

        // Silk.NET Input: Keyboard
        if (s_inputcontext.Keyboards != null && s_inputcontext.Keyboards.Count != 0)
            _primaryKeyboard = s_inputcontext.Keyboards[0];
        if (_primaryKeyboard == null)
            throw new DotNet6502Exception("Keyboard not found");
        ListenForKeyboardInput();

        // Silk.NET Input: Gamepad
        if (s_inputcontext.Gamepads != null && s_inputcontext.Gamepads.Count != 0)
        {
            _currentGamePad = s_inputcontext.Gamepads[0];
            ListenForGamepadInput();
        }
        else
        {
            _logger.LogInformation("No gamepads found.");
        }

        IsInitialized = true;
    }

    private void ConnectionChanged(IInputDevice device, bool isConnected)
    {
        _logger.LogInformation($"Input Connection Changed: {device.Name} {device.Index} isConnected: {isConnected}");
        if (device is Silk.NET.Input.IGamepad gamepad)
        {
            if (isConnected)
            {
                _currentGamePad = gamepad;
                ListenForGamepadInput();
                _logger.LogInformation($"Current Gamepad is now: {device.Name} {device.Index}");
            }
            else
            {
                _currentGamePad = null;
            }
        }
        //else if (device is IKeyboard keyboard)
        //{
        //    if (isConnected)
        //    {
        //        _primaryKeyboard = keyboard;
        //        ListenForKeyboardInput();
        //    }
        //    else
        //    {
        //        _primaryKeyboard = null;
        //    }
        //}
        //else
        //{
        //    _logger.LogWarning($"Unknown device type: {device.GetType().Name}");
        //}   

    }

    private void ListenForGamepadInput()
    {
        if (_currentGamePad == null)
            return;

        _currentGamePad.Deadzone = new Deadzone(0.05f, DeadzoneMethod.Traditional);

        // Unregister any existing handlers to avoid duplicates
        _currentGamePad.ButtonDown -= GamepadButtonDown;
        _currentGamePad.ButtonUp -= GamepadButtonUp;
        //_currentGamePad.ThumbstickMoved -= GamepadThumbstickMoved;
        //_currentGamePad.TriggerMoved -= GamepadTriggerMoved;

        _currentGamePad.ButtonDown += GamepadButtonDown;
        _currentGamePad.ButtonUp += GamepadButtonUp;
        //_currentGamePad.ThumbstickMoved += GamepadThumbstickMoved;
        //_currentGamePad.TriggerMoved += GamepadTriggerMoved;
    }

    //private void GamepadTriggerMoved(IGamepad gamepad, Trigger trigger)
    //{
    //    _logger.LogDebug($"GamepadTriggerMoved: {trigger.Index}");
    //}

    //private void GamepadThumbstickMoved(IGamepad gamepad, Thumbstick thumbstick)
    //{
    //    _logger.LogDebug($"GamepadThumbstickMoved: {thumbstick.Index} {thumbstick.X},{thumbstick.Y}");
    //}

    private void GamepadButtonUp(Silk.NET.Input.IGamepad gamepad, Button button)
    {
        var buttonId = button.Name;
        if (GamepadButtonsDown.Contains(buttonId))
        {
            _logger.LogDebug($"GamepadButtonUp: {button.Name} {button.Index} {button.Pressed}");
            GamepadButtonsDown.Remove(buttonId);
        }
    }

    private void GamepadButtonDown(Silk.NET.Input.IGamepad gamepad, Button button)
    {
        var buttonId = button.Name;
        if (!GamepadButtonsDown.Contains(buttonId))
        {
            _logger.LogDebug($"GamepadButtonDown: {button.Name} {button.Index} {button.Pressed}");
            GamepadButtonsDown.Add(buttonId);
        }
    }

    public void ListenForKeyboardInput(bool enabled = true)
    {
        // Unregister any existing handlers to avoid duplicates
        _primaryKeyboard.KeyUp -= KeyUp;
        _primaryKeyboard.KeyDown -= KeyDown;

        if (!enabled)
            return;
        _primaryKeyboard.KeyUp += KeyUp;
        _primaryKeyboard.KeyDown += KeyDown;
    }

    private void KeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        if (KeysDown.Contains(key))
        {
            _logger.LogDebug($"Host KeyUp event: {key} ({scanCode})");
            KeysDown.Remove(key);
        }

        if (key == Key.CapsLock)
        {
            _capsLockKeyDownCaptured = false;
        }
    }

    private void KeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        if (!KeysDown.Contains(key))
        {
            _logger.LogDebug($"Host KeyDown event: {key} ({scanCode})");
            KeysDown.Add(key);
        }

        if (key == Key.CapsLock && !_capsLockKeyDownCaptured)
        {
            _capsLockKeyDownCaptured = true;
            _capsLockOn = !_capsLockOn; // Toggle state
        }
    }

    public bool GetCapsLockState()
    {
        // On Windows, Console.CapsLock can be used to check if CapsLock is on.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Console.CapsLock;

        // On Linux and Mac return our own captured caps lock state (which may be wrong if the user has pressed the caps lock key outside of the emulator).
        return _capsLockOn;
    }

    public void Cleanup()
    {
        s_inputcontext?.Dispose();
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
                var hostKey = MapToHostKey(key);
                if (hostKey != HostKey.None)
                    result.Add(hostKey);
            }
            return result;
        }
    }

    IReadOnlySet<GamepadButton> IHostInputState.GamepadButtonsDown
    {
        get
        {
            var result = new HashSet<GamepadButton>();
            foreach (var button in GamepadButtonsDown)
            {
                var gamepadButton = MapToGamepadButton(button);
                if (gamepadButton.HasValue)
                    result.Add(gamepadButton.Value);
            }
            return result;
        }
    }

    bool IHostInputState.CapsLockOn => GetCapsLockState();

    void IHostInputState.UpdatePerFrame()
    {
        // Silk.NET delivers keyboard/gamepad state via events; nothing to poll per frame.
    }

    private static HostKey MapToHostKey(Key key) => key switch
    {
        Key.A => HostKey.KeyA, Key.B => HostKey.KeyB, Key.C => HostKey.KeyC,
        Key.D => HostKey.KeyD, Key.E => HostKey.KeyE, Key.F => HostKey.KeyF,
        Key.G => HostKey.KeyG, Key.H => HostKey.KeyH, Key.I => HostKey.KeyI,
        Key.J => HostKey.KeyJ, Key.K => HostKey.KeyK, Key.L => HostKey.KeyL,
        Key.M => HostKey.KeyM, Key.N => HostKey.KeyN, Key.O => HostKey.KeyO,
        Key.P => HostKey.KeyP, Key.Q => HostKey.KeyQ, Key.R => HostKey.KeyR,
        Key.S => HostKey.KeyS, Key.T => HostKey.KeyT, Key.U => HostKey.KeyU,
        Key.V => HostKey.KeyV, Key.W => HostKey.KeyW, Key.X => HostKey.KeyX,
        Key.Y => HostKey.KeyY, Key.Z => HostKey.KeyZ,
        Key.Number0 => HostKey.Digit0, Key.Number1 => HostKey.Digit1,
        Key.Number2 => HostKey.Digit2, Key.Number3 => HostKey.Digit3,
        Key.Number4 => HostKey.Digit4, Key.Number5 => HostKey.Digit5,
        Key.Number6 => HostKey.Digit6, Key.Number7 => HostKey.Digit7,
        Key.Number8 => HostKey.Digit8, Key.Number9 => HostKey.Digit9,
        Key.Space => HostKey.Space, Key.Enter => HostKey.Enter, Key.Tab => HostKey.Tab,
        Key.Backspace => HostKey.Backspace, Key.Escape => HostKey.Escape,
        Key.F1 => HostKey.F1, Key.F2 => HostKey.F2, Key.F3 => HostKey.F3,
        Key.F4 => HostKey.F4, Key.F5 => HostKey.F5, Key.F6 => HostKey.F6,
        Key.F7 => HostKey.F7, Key.F8 => HostKey.F8, Key.F9 => HostKey.F9,
        Key.F10 => HostKey.F10, Key.F11 => HostKey.F11, Key.F12 => HostKey.F12,
        Key.Insert => HostKey.Insert, Key.Delete => HostKey.Delete,
        Key.Home => HostKey.Home, Key.End => HostKey.End,
        Key.PageUp => HostKey.PageUp, Key.PageDown => HostKey.PageDown,
        Key.Up => HostKey.ArrowUp, Key.Down => HostKey.ArrowDown,
        Key.Left => HostKey.ArrowLeft, Key.Right => HostKey.ArrowRight,
        Key.ShiftLeft => HostKey.ShiftLeft, Key.ShiftRight => HostKey.ShiftRight,
        Key.ControlLeft => HostKey.ControlLeft, Key.ControlRight => HostKey.ControlRight,
        Key.AltLeft => HostKey.AltLeft, Key.AltRight => HostKey.AltRight,
        Key.SuperLeft => HostKey.MetaLeft, Key.SuperRight => HostKey.MetaRight,
        Key.CapsLock => HostKey.CapsLock,
        Key.GraveAccent => HostKey.Backquote, Key.Minus => HostKey.Minus,
        Key.Equal => HostKey.Equal, Key.LeftBracket => HostKey.BracketLeft,
        Key.RightBracket => HostKey.BracketRight, Key.BackSlash => HostKey.Backslash,
        Key.Semicolon => HostKey.Semicolon, Key.Apostrophe => HostKey.Quote,
        Key.Comma => HostKey.Comma, Key.Period => HostKey.Period, Key.Slash => HostKey.Slash,
        Key.World2 => HostKey.IntlBackslash,
        _ => HostKey.None,
    };

    private static GamepadButton? MapToGamepadButton(ButtonName button) => button switch
    {
        ButtonName.A => GamepadButton.A, ButtonName.B => GamepadButton.B,
        ButtonName.X => GamepadButton.X, ButtonName.Y => GamepadButton.Y,
        ButtonName.LeftBumper => GamepadButton.LeftBumper,
        ButtonName.RightBumper => GamepadButton.RightBumper,
        ButtonName.Back => GamepadButton.Back, ButtonName.Start => GamepadButton.Start,
        ButtonName.Home => GamepadButton.Guide,
        ButtonName.LeftStick => GamepadButton.LeftStick,
        ButtonName.RightStick => GamepadButton.RightStick,
        ButtonName.DPadUp => GamepadButton.DPadUp, ButtonName.DPadDown => GamepadButton.DPadDown,
        ButtonName.DPadLeft => GamepadButton.DPadLeft, ButtonName.DPadRight => GamepadButton.DPadRight,
        _ => null,
    };
}
