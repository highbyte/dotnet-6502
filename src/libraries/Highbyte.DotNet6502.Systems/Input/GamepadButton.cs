namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// Represents standard gamepad buttons.
/// This is an abstraction that can be mapped from browser gamepad API or desktop SDL2 gamepad API.
/// The button names are based on the XInput/Xbox controller layout.
/// </summary>
public enum GamepadButton
{
    /// <summary>A button (bottom face button)</summary>
    A = 0,
    /// <summary>B button (right face button)</summary>
    B = 1,
    /// <summary>X button (left face button)</summary>
    X = 2,
    /// <summary>Y button (top face button)</summary>
    Y = 3,
    /// <summary>Left bumper/shoulder button</summary>
    LeftBumper = 4,
    /// <summary>Right bumper/shoulder button</summary>
    RightBumper = 5,
    /// <summary>Back/Select button</summary>
    Back = 6,
    /// <summary>Start button</summary>
    Start = 7,
    /// <summary>Left stick button (pressed)</summary>
    LeftStick = 8,
    /// <summary>Right stick button (pressed)</summary>
    RightStick = 9,
    /// <summary>D-Pad Up</summary>
    DPadUp = 10,
    /// <summary>D-Pad Down</summary>
    DPadDown = 11,
    /// <summary>D-Pad Left</summary>
    DPadLeft = 12,
    /// <summary>D-Pad Right</summary>
    DPadRight = 13,
    /// <summary>Guide/Home button</summary>
    Guide = 14,
    /// <summary>Left trigger (as button)</summary>
    LeftTrigger = 15,
    /// <summary>Right trigger (as button)</summary>
    RightTrigger = 16,
}
