using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using Toolbelt.Blazor.Gamepad;

namespace Highbyte.DotNet6502.Impl.AspNet;

public class AspNetInputHandlerContext : IInputHandlerContext
{
    private readonly ILogger<AspNetInputHandlerContext> _logger;

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

    public AspNetInputHandlerContext(ILoggerFactory loggerFactory, GamepadList gamepadList)
    {
        _logger = loggerFactory.CreateLogger<AspNetInputHandlerContext>();
        _gamepadList = gamepadList;
    }

    public void Init()
    {
        _gamepadUpdateTimer.Elapsed += GamepadUpdateTimer_Elapsed;
        _gamepadConnectCheckTimer.Elapsed += GamepadConectCheckTimer_Elapsed;
    }

    private async void GamepadConectCheckTimer_Elapsed(object sender, EventArgs args)
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

    private async void GamepadUpdateTimer_Elapsed(object sender, EventArgs args)
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

    public void Dispose()
    {
        _gamepadUpdateTimer.Dispose();
    }
}
