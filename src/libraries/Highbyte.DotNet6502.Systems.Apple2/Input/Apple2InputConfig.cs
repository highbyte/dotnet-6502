using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Apple2.Input;

/// <summary>
/// How host input drives the Apple II game port: which gamepad buttons and which host keys count
/// as joystick directions.
///
/// The machine has a single game port, so unlike the C64 there is no port to choose between —
/// paddles 0 and 1 are the one stick's axes.
/// </summary>
public class Apple2InputConfig : ICloneable
{
    /// <summary>
    /// The host physical keyboard layout the Apple II keyboard mapping assumes. Selects which
    /// punctuation and shifted-digit map <see cref="Apple2HostKeyboard"/> merges in.
    /// <para>
    /// <c>null</c> — the default, and what an absent or empty <c>appsettings.json</c> value binds
    /// to — means <em>auto-detect</em>: the input handler resolves the layout from the host's
    /// detected keyboard layout, then the OS culture, then falls back to
    /// <see cref="HostKeyboardLayout.US"/>. A non-null value forces that layout.
    /// </para>
    /// <para>
    /// A property (not a field) so it binds from <c>appsettings.json</c>; the string-enum
    /// converter keeps the persisted JSON readable (e.g. <c>"Swedish"</c> rather than a number).
    /// </para>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<HostKeyboardLayout>))]
    public HostKeyboardLayout? KeyboardLayout { get; set; }

    /// <summary>
    /// Whether host keys drive the joystick. Off by default, and deliberately opt-in: the mapped
    /// keys are taken away from the keyboard while it is on.
    /// </summary>
    public bool KeyboardJoystickEnabled { get; set; }

    /// <summary>
    /// Which host key stands for which joystick action, when the keyboard joystick is on.
    ///
    /// WASD rather than the arrows, which keeps the Apple II's own left/right arrows — backspace
    /// and retype — working while the stick is in use. Left Shift is the second button, so Right
    /// Shift is still available for typing shifted characters.
    /// </summary>
    public Dictionary<HostKey, JoystickAction> KeyboardToJoystickMap = new()
    {
        { HostKey.KeyW, JoystickAction.Up },
        { HostKey.KeyS, JoystickAction.Down },
        { HostKey.KeyA, JoystickAction.Left },
        { HostKey.KeyD, JoystickAction.Right },
        { HostKey.Space, JoystickAction.Fire },
        { HostKey.ShiftLeft, JoystickAction.Fire2 },
    };

    /// <summary>
    /// Which gamepad button combination triggers which joystick action. Same shape as the C64's
    /// map, and the same defaults, so a controller behaves identically across systems.
    /// </summary>
    public Dictionary<GamepadButton[], JoystickAction[]> GamePadToJoystickMap = new()
    {
        { new[] { GamepadButton.A }, new[] { JoystickAction.Fire } },
        { new[] { GamepadButton.B }, new[] { JoystickAction.Fire2 } },
        { new[] { GamepadButton.DPadUp }, new[] { JoystickAction.Up } },
        { new[] { GamepadButton.DPadDown }, new[] { JoystickAction.Down } },
        { new[] { GamepadButton.DPadLeft }, new[] { JoystickAction.Left } },
        { new[] { GamepadButton.DPadRight }, new[] { JoystickAction.Right } },
    };

    public object Clone() => MemberwiseClone();
}
