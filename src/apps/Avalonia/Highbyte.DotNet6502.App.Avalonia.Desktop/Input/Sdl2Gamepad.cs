using System;
using System.Collections.Generic;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Microsoft.Extensions.Logging;
using Silk.NET.SDL;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop.Input;

/// <summary>
/// SDL2 implementation of IAvaloniaGamepad for desktop platforms.
/// Uses Silk.NET.SDL Game Controller API for gamepad input.
/// </summary>
public unsafe class Sdl2Gamepad : IAvaloniaGamepad
{
    private readonly ILogger<Sdl2Gamepad> _logger;
    private Sdl? _sdl;
    private GameController* _gameController;
    private bool _sdlInitialized = false;

    public bool IsInitialized { get; private set; }
    public bool IsConnected => _gameController != null;
    public string? GamepadName { get; private set; }
    public HashSet<GamepadButton> ButtonsDown { get; } = new();

    public Sdl2Gamepad(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<Sdl2Gamepad>();
    }

    public void Init()
    {
        if (IsInitialized)
            return;

        try
        {
            // Get SDL API instance
            _sdl = Sdl.GetApi();

            // Initialize SDL2 with GameController subsystem
            if (_sdl.Init(Sdl.InitGamecontroller) < 0)
            {
                var error = _sdl.GetErrorS();
                _logger.LogError("Failed to initialize SDL2 GameController: {Error}", error);
                return;
            }

            _sdlInitialized = true;
            _logger.LogInformation("SDL2 GameController subsystem initialized");

            // Try to find and open a connected controller
            DetectAndOpenController();

            IsInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during SDL2 gamepad initialization");
        }
    }

    private void DetectAndOpenController()
    {
        if (_sdl == null)
            return;

        int numJoysticks = _sdl.NumJoysticks();
        _logger.LogInformation("Found {Count} joystick(s)", numJoysticks);

        for (int i = 0; i < numJoysticks; i++)
        {
            if (_sdl.IsGameController(i) == SdlBool.True)
            {
                OpenController(i);
                if (_gameController != null)
                    break;
            }
        }

        if (_gameController == null)
        {
            _logger.LogInformation("No game controller found");
        }
    }

    private void OpenController(int index)
    {
        if (_sdl == null)
            return;

        if (_gameController != null)
        {
            _sdl.GameControllerClose(_gameController);
            _gameController = null;
        }

        _gameController = _sdl.GameControllerOpen(index);
        if (_gameController != null)
        {
            GamepadName = _sdl.GameControllerNameS(_gameController);
            _logger.LogInformation("Opened game controller: {Name} at index {Index}", GamepadName, index);
        }
        else
        {
            var error = _sdl.GetErrorS();
            _logger.LogWarning("Failed to open game controller at index {Index}: {Error}", index, error);
        }
    }

    public void Update()
    {
        if (!IsInitialized || !_sdlInitialized || _sdl == null)
            return;

        // Pump SDL events to update controller state
        _sdl.GameControllerUpdate();

        // Check for controller connect/disconnect
        CheckControllerConnection();

        if (_gameController == null)
            return;

        // Update button states
        ButtonsDown.Clear();

        // Check all buttons
        CheckButton(GameControllerButton.A, GamepadButton.A);
        CheckButton(GameControllerButton.B, GamepadButton.B);
        CheckButton(GameControllerButton.X, GamepadButton.X);
        CheckButton(GameControllerButton.Y, GamepadButton.Y);
        CheckButton(GameControllerButton.Leftshoulder, GamepadButton.LeftBumper);
        CheckButton(GameControllerButton.Rightshoulder, GamepadButton.RightBumper);
        CheckButton(GameControllerButton.Back, GamepadButton.Back);
        CheckButton(GameControllerButton.Start, GamepadButton.Start);
        CheckButton(GameControllerButton.Leftstick, GamepadButton.LeftStick);
        CheckButton(GameControllerButton.Rightstick, GamepadButton.RightStick);
        CheckButton(GameControllerButton.DpadUp, GamepadButton.DPadUp);
        CheckButton(GameControllerButton.DpadDown, GamepadButton.DPadDown);
        CheckButton(GameControllerButton.DpadLeft, GamepadButton.DPadLeft);
        CheckButton(GameControllerButton.DpadRight, GamepadButton.DPadRight);
        CheckButton(GameControllerButton.Guide, GamepadButton.Guide);

        // Check triggers as buttons (threshold-based)
        CheckTriggerAsButton(GameControllerAxis.Triggerleft, GamepadButton.LeftTrigger);
        CheckTriggerAsButton(GameControllerAxis.Triggerright, GamepadButton.RightTrigger);

        // Also check left stick for D-Pad emulation
        CheckStickAsButtons();
    }

    private void CheckControllerConnection()
    {
        if (_sdl == null)
            return;

        // Check if current controller is still attached
        if (_gameController != null)
        {
            if (_sdl.GameControllerGetAttached(_gameController) == SdlBool.False)
            {
                _logger.LogInformation("Game controller disconnected: {Name}", GamepadName);
                _sdl.GameControllerClose(_gameController);
                _gameController = null;
                GamepadName = null;
                ButtonsDown.Clear();
            }
        }

        // If no controller, try to detect one
        if (_gameController == null)
        {
            int numJoysticks = _sdl.NumJoysticks();
            for (int i = 0; i < numJoysticks; i++)
            {
                if (_sdl.IsGameController(i) == SdlBool.True)
                {
                    OpenController(i);
                    if (_gameController != null)
                        break;
                }
            }
        }
    }

    private void CheckButton(GameControllerButton sdlButton, GamepadButton button)
    {
        if (_sdl == null || _gameController == null)
            return;

        if (_sdl.GameControllerGetButton(_gameController, sdlButton) == 1)
        {
            ButtonsDown.Add(button);
        }
    }

    private void CheckTriggerAsButton(GameControllerAxis axis, GamepadButton button)
    {
        if (_sdl == null || _gameController == null)
            return;

        // SDL trigger values are 0-32767, consider pressed if > 50% (16384)
        const short TriggerThreshold = 16384;
        short value = _sdl.GameControllerGetAxis(_gameController, axis);
        if (value > TriggerThreshold)
        {
            ButtonsDown.Add(button);
        }
    }

    private void CheckStickAsButtons()
    {
        if (_sdl == null || _gameController == null)
            return;

        // SDL stick values are -32768 to 32767
        const short StickThreshold = 16384; // ~50% of max

        short leftX = _sdl.GameControllerGetAxis(_gameController, GameControllerAxis.Leftx);
        short leftY = _sdl.GameControllerGetAxis(_gameController, GameControllerAxis.Lefty);

        // Only add stick directions if D-Pad is not already pressed (D-Pad takes priority)
        if (!ButtonsDown.Contains(GamepadButton.DPadLeft) && !ButtonsDown.Contains(GamepadButton.DPadRight))
        {
            if (leftX < -StickThreshold)
                ButtonsDown.Add(GamepadButton.DPadLeft);
            else if (leftX > StickThreshold)
                ButtonsDown.Add(GamepadButton.DPadRight);
        }

        if (!ButtonsDown.Contains(GamepadButton.DPadUp) && !ButtonsDown.Contains(GamepadButton.DPadDown))
        {
            if (leftY < -StickThreshold)
                ButtonsDown.Add(GamepadButton.DPadUp);
            else if (leftY > StickThreshold)
                ButtonsDown.Add(GamepadButton.DPadDown);
        }
    }

    public bool IsButtonDown(GamepadButton button) => ButtonsDown.Contains(button);

    public void Cleanup()
    {
        if (_sdl != null && _gameController != null)
        {
            _sdl.GameControllerClose(_gameController);
            _gameController = null;
            GamepadName = null;
        }

        if (_sdlInitialized && _sdl != null)
        {
            _sdl.QuitSubSystem(Sdl.InitGamecontroller);
            _sdlInitialized = false;
        }

        ButtonsDown.Clear();
        IsInitialized = false;

        _sdl?.Dispose();
        _sdl = null;
    }

    public void Dispose()
    {
        Cleanup();
    }
}
