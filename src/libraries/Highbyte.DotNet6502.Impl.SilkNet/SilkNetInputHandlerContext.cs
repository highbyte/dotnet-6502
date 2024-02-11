using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using Silk.NET.SDL;

namespace Highbyte.DotNet6502.Impl.SilkNet;

public class SilkNetInputHandlerContext : IInputHandlerContext
{
    private readonly IWindow _silkNetWindow;
    private readonly ILogger<SilkNetInputHandlerContext> _logger;
    private static IInputContext s_inputcontext = default!;
    public IInputContext InputContext => s_inputcontext;

    // Keyboard
    private IKeyboard _primaryKeyboard = default!;
    public IKeyboard PrimaryKeyboard => _primaryKeyboard;
    public HashSet<Key> KeysDown = new();
    private bool _capsLockKeyDownCaptured;
    private bool _capsLockOn;

    // Gamepad
    private IGamepad? _currentGamePad;
    public IGamepad? CurrentGamePad => _currentGamePad;
    public HashSet<ButtonName> GamepadButtonsDown = new();

    public bool Quit { get; private set; }

    public bool IsKeyPressed(Key key) => _primaryKeyboard.IsKeyPressed(key);

    public SilkNetInputHandlerContext(IWindow silkNetWindow, ILoggerFactory loggerFactory)
    {
        _silkNetWindow = silkNetWindow;
        _logger = loggerFactory.CreateLogger<SilkNetInputHandlerContext>();
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
            ListenForGampadInput();
        }
        else
        {
            _logger.LogInformation("No gamepads found.");
        }
    }

    private void ConnectionChanged(IInputDevice device, bool isConnected)
    {
        _logger.LogInformation($"Input Connection Changed: {device.Name} {device.Index} isConnected: {isConnected}");
        if (device is IGamepad gamepad)
        {
            if (isConnected)
            {
                _currentGamePad = gamepad;
                ListenForGampadInput();
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

    private void ListenForGampadInput()
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

    private void GamepadButtonUp(IGamepad gamepad, Button button)
    {
        var buttonId = button.Name;
        if (GamepadButtonsDown.Contains(buttonId))
        {
            _logger.LogDebug($"GamepadButtonUp: {button.Name} {button.Index} {button.Pressed}");
            GamepadButtonsDown.Remove(buttonId);
        }
    }

    private void GamepadButtonDown(IGamepad gamepad, Button button)
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
}
