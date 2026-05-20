using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging;
using Toolbelt.Blazor.Gamepad;
using GamepadButton = Highbyte.DotNet6502.Systems.Input.GamepadButton;

namespace Highbyte.DotNet6502.Impl.AspNet;

public class AspNetInputHandlerContext : IInputHandlerContext, IHostInputState
{
    private readonly ILogger _logger;

    // Keyboard
    public HashSet<string> KeysDown = new();
    private bool _capsLockKeyDownCaptured;
    private bool _capsLockOn;

    // Gamepad
    private readonly GamepadList _gamepadList;
    private readonly System.Timers.Timer _gamepadUpdateTimer = new System.Timers.Timer(50) { Enabled = true };
    private readonly System.Timers.Timer _gamepadConnectCheckTimer = new System.Timers.Timer(1000) { Enabled = true };
    private Gamepad? _currentGamepad;
    public HashSet<int> GamepadButtonsDown = new();
    public bool IsInitialized { get; private set; }

    // Whether the browser is running on macOS. Detected by the host (the .NET runtime reports
    // OSPlatform.Browser in WASM, not the underlying OS); see IHostInputState.IsRunningOnMacOS.
    private readonly bool _isRunningOnMacOS;

    // The browser-detected native keyboard layout id, or null when not detectable. Supplied by
    // the host (the .NET runtime cannot query the OS in WASM); see DetectNativeKeyboardLayoutId.
    private readonly string? _detectedKeyboardLayoutId;

    public AspNetInputHandlerContext(
        ILoggerFactory loggerFactory,
        GamepadList gamepadList,
        bool isRunningOnMacOS = false,
        string? detectedKeyboardLayoutId = null)
    {
        _logger = loggerFactory.CreateLogger(nameof(AspNetInputHandlerContext));
        _gamepadList = gamepadList;
        _isRunningOnMacOS = isRunningOnMacOS;
        _detectedKeyboardLayoutId = detectedKeyboardLayoutId;
    }

    public void Init()
    {
        _gamepadUpdateTimer.Elapsed += GamepadUpdateTimer_Elapsed;
        _gamepadConnectCheckTimer.Elapsed += GamepadConectCheckTimer_Elapsed;
        IsInitialized = true;
    }

    private async void GamepadConectCheckTimer_Elapsed(object? sender, EventArgs args)
    {
        await DetectConnectedGamepad();
    }
    private async Task DetectConnectedGamepad()
    {
        try
        {
            var gamepads = await _gamepadList.GetGamepadsAsync();

            var gamePad = gamepads.FirstOrDefault(gp => gp.Id.Contains("xbox 360", StringComparison.InvariantCultureIgnoreCase))
                ?? gamepads.LastOrDefault();
            if (gamePad != _currentGamepad)
            {
                _currentGamepad = gamePad;
                if (_currentGamepad != null && _currentGamepad.Connected)
                    _logger.LogInformation($"Current gamepad changed to: {_currentGamepad.Id} ({_currentGamepad.Index})");
                else
                    _logger.LogInformation($"Gamepad disconnected");
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(e.ToString());
            throw;
        }
    }

    private void GamepadUpdateTimer_Elapsed(object? sender, EventArgs args)
    {
        try
        {
            GamepadButtonsDown.Clear();

            if (_currentGamepad == null || !_currentGamepad.Connected)
                return;

            for (int buttonIndex = 0; buttonIndex < _currentGamepad.Buttons.Count; buttonIndex++)
            {
                var button = _currentGamepad.Buttons[buttonIndex];
                if (!button.Pressed)
                    continue;
                GamepadButtonsDown.Add(buttonIndex);
                _logger.LogInformation($"Gamepad button pressed: {buttonIndex} ({button.Pressed})");
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(e.ToString());
            throw;
        }
    }

    public void KeyUp(KeyboardEventArgs e)
    {
        if (KeysDown.Contains(e.Code))
        {
            _logger.LogDebug($"Host KeyUp event: {e.Key} ({e.Code})");
            KeysDown.Remove(e.Code);
        }

        if (e.Code == "CapsLock")
        {
            _capsLockKeyDownCaptured = false;
        }
    }

    public void KeyDown(KeyboardEventArgs e)
    {
        if (!KeysDown.Contains(e.Code))
        {
            _logger.LogDebug($"Host KeyDown event: {e.Key} ({e.Code})");
            KeysDown.Add(e.Code);
        }

        if (e.Code == "CapsLock" && !_capsLockKeyDownCaptured)
        {
            _capsLockKeyDownCaptured = true;
            _capsLockOn = !_capsLockOn; // Toggle state
        }
    }

    public void OnFocus(FocusEventArgs e)
    {
        _logger.LogDebug($"Host OnFocus event");
        KeysDown.Clear();
    }

    public bool GetCapsLockState()
    {
        // TODO: Is there a built-in way in Javascript/WASM to check if CapsLock is on?
        //       That could improve the custom detection in this code, which might not match the actual caps lock state of the user. 
        return _capsLockOn;
    }

    public void Cleanup()
    {
    }

    // ----------------------------------------------------------------------
    // IHostInputState: neutral input surface consumed by system input handlers.
    // ----------------------------------------------------------------------

    IReadOnlySet<HostKey> IHostInputState.KeysDown
    {
        get
        {
            // The host KeysDown strings are W3C KeyboardEvent.code values, whose names match
            // the HostKey member names — so a direct enum parse is the translation.
            var result = new HashSet<HostKey>();
            foreach (var code in KeysDown)
            {
                if (Enum.TryParse<HostKey>(code, ignoreCase: false, out var hostKey) && hostKey != HostKey.None)
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
            foreach (var buttonIndex in GamepadButtonsDown)
            {
                var gamepadButton = MapToGamepadButton(buttonIndex);
                if (gamepadButton.HasValue)
                    result.Add(gamepadButton.Value);
            }
            return result;
        }
    }

    bool IHostInputState.CapsLockOn => GetCapsLockState();

    // Overridden because the .NET runtime reports OSPlatform.Browser (not the underlying OS) in
    // WASM, so the interface default cannot detect a browser running on macOS.
    bool IHostInputState.IsRunningOnMacOS => _isRunningOnMacOS;

    // Overridden because the .NET runtime cannot query the OS keyboard layout in WASM; the host
    // detects it from the browser (Keyboard Map API) and passes it to the constructor.
    string? IHostInputState.DetectNativeKeyboardLayoutId() => _detectedKeyboardLayoutId;

    void IHostInputState.UpdatePerFrame()
    {
        // Keyboard arrives via Blazor events; the gamepad is polled by an internal timer.
    }

    // Browser Gamepad API "standard" mapping: button index to neutral GamepadButton.
    private static GamepadButton? MapToGamepadButton(int buttonIndex) => buttonIndex switch
    {
        0 => GamepadButton.A, 1 => GamepadButton.B, 2 => GamepadButton.X, 3 => GamepadButton.Y,
        4 => GamepadButton.LeftBumper, 5 => GamepadButton.RightBumper,
        6 => GamepadButton.LeftTrigger, 7 => GamepadButton.RightTrigger,
        8 => GamepadButton.Back, 9 => GamepadButton.Start,
        10 => GamepadButton.LeftStick, 11 => GamepadButton.RightStick,
        12 => GamepadButton.DPadUp, 13 => GamepadButton.DPadDown,
        14 => GamepadButton.DPadLeft, 15 => GamepadButton.DPadRight,
        16 => GamepadButton.Guide,
        _ => null,
    };

    public void Dispose()
    {
        _gamepadUpdateTimer.Dispose();
    }
}
