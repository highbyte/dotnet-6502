using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Commodore64.Input;

/// <summary>
/// 
/// Host-agnostic C64 gamepad/joystick configuration: which C64 joystick port a host gamepad
/// drives, and the gamepad-button to C64-joystick-action mapping.
///
/// Single replacement for the formerly duplicated per-host input configs
/// (<c>C64SilkNetInputConfig</c>, <c>C64AvaloniaInputConfig</c>, <c>C64AspNetInputConfig</c>).
/// The mapping is keyed by the neutral <see cref="GamepadButton"/> abstraction.
/// </summary>
public class C64InputConfig : ICloneable
{
    /// <summary>
    /// The host physical keyboard layout the C64 keyboard mapping assumes. Selects which
    /// layout-specific punctuation map <see cref="C64HostKeyboard"/> uses.
    /// <para>
    /// <c>null</c> — the default, and what an absent or empty <c>appsettings.json</c> value binds
    /// to — means <em>auto-detect</em>: the C64 input handler resolves the layout from the host's
    /// detected keyboard layout, then the OS culture, then falls back to
    /// <see cref="C64KeyboardLayout.US"/>. A non-null value forces that layout.
    /// </para>
    /// <para>
    /// A property (not a field) so it binds from <c>appsettings.json</c> via
    /// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>; the string-enum converter
    /// keeps the persisted JSON readable (e.g. <c>"Swedish"</c> rather than a number).
    /// </para>
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<C64KeyboardLayout>))]
    public C64KeyboardLayout? KeyboardLayout { get; set; }

    /// <summary>The C64 joystick port (1 or 2) that the host gamepad currently drives.</summary>
    public int CurrentJoystick = 2;

    /// <summary>The selectable C64 joystick ports.</summary>
    public List<int> AvailableJoysticks = new() { 1, 2 };

    /// <summary>
    /// Per C64 joystick port: which host keyboard key maps to which C64 joystick action, for
    /// using the host keyboard as a joystick. Keyed by the neutral <see cref="HostKey"/> abstraction.
    /// </summary>
    public Dictionary<int, Dictionary<HostKey, C64JoystickAction>> KeyboardToC64JoystickMap = new()
    {
        {
            1,
            new Dictionary<HostKey, C64JoystickAction>
            {
                { HostKey.Space, C64JoystickAction.Fire },
                { HostKey.ArrowUp, C64JoystickAction.Up },
                { HostKey.ArrowDown, C64JoystickAction.Down },
                { HostKey.ArrowLeft, C64JoystickAction.Left },
                { HostKey.ArrowRight, C64JoystickAction.Right },
            }
        },
        {
            2,
            new Dictionary<HostKey, C64JoystickAction>
            {
                { HostKey.ControlLeft, C64JoystickAction.Fire },
                { HostKey.KeyW, C64JoystickAction.Up },
                { HostKey.KeyS, C64JoystickAction.Down },
                { HostKey.KeyA, C64JoystickAction.Left },
                { HostKey.KeyD, C64JoystickAction.Right },
            }
        }
    };

    /// <summary>
    /// Per C64 joystick port: which gamepad-button combination triggers which C64 joystick action.
    /// </summary>
    public Dictionary<int, Dictionary<GamepadButton[], C64JoystickAction[]>> GamePadToC64JoystickMap = new()
    {
        {
            1,
            new Dictionary<GamepadButton[], C64JoystickAction[]>
            {
                { new[] { GamepadButton.A }, new[] { C64JoystickAction.Fire } },
                { new[] { GamepadButton.DPadUp }, new[] { C64JoystickAction.Up } },
                { new[] { GamepadButton.DPadDown }, new[] { C64JoystickAction.Down } },
                { new[] { GamepadButton.DPadLeft }, new[] { C64JoystickAction.Left } },
                { new[] { GamepadButton.DPadRight }, new[] { C64JoystickAction.Right } },
            }
        },
        {
            2,
            new Dictionary<GamepadButton[], C64JoystickAction[]>
            {
                { new[] { GamepadButton.A }, new[] { C64JoystickAction.Fire } },
                { new[] { GamepadButton.DPadUp }, new[] { C64JoystickAction.Up } },
                { new[] { GamepadButton.DPadDown }, new[] { C64JoystickAction.Down } },
                { new[] { GamepadButton.DPadLeft }, new[] { C64JoystickAction.Left } },
                { new[] { GamepadButton.DPadRight }, new[] { C64JoystickAction.Right } },
            }
        }
    };

    public object Clone()
    {
        return MemberwiseClone();
    }
}
