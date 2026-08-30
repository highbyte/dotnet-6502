using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Oric.Input;

/// <summary>Host-agnostic gamepad and keyboard-joystick mappings for the Oric.</summary>
public sealed class OricInputConfig : ICloneable
{
    private static readonly IReadOnlyList<int> s_availableJoysticks = [1, 2];

    private static readonly IReadOnlyDictionary<HostKey, JoystickAction> s_keyboardJoystickMap =
        new Dictionary<HostKey, JoystickAction>
        {
            [HostKey.Space] = JoystickAction.Fire,
            [HostKey.KeyW] = JoystickAction.Up,
            [HostKey.KeyS] = JoystickAction.Down,
            [HostKey.KeyA] = JoystickAction.Left,
            [HostKey.KeyD] = JoystickAction.Right,
        };

    private static readonly IReadOnlyDictionary<GamepadButton, JoystickAction> s_gamepadMap =
        new Dictionary<GamepadButton, JoystickAction>
        {
            [GamepadButton.A] = JoystickAction.Fire,
            [GamepadButton.DPadUp] = JoystickAction.Up,
            [GamepadButton.DPadDown] = JoystickAction.Down,
            [GamepadButton.DPadLeft] = JoystickAction.Left,
            [GamepadButton.DPadRight] = JoystickAction.Right,
        };

    /// <summary>The Oric adapter socket (1 or 2) driven by the host gamepad.</summary>
    public int CurrentJoystick { get; set; } = 1;

    [JsonIgnore]
    public IReadOnlyList<int> AvailableJoysticks => s_availableJoysticks;

    [JsonIgnore]
    public IReadOnlyDictionary<HostKey, JoystickAction> KeyboardJoystickMap => s_keyboardJoystickMap;

    [JsonIgnore]
    public IReadOnlyDictionary<GamepadButton, JoystickAction> GamepadMap => s_gamepadMap;

    public object Clone() => MemberwiseClone();
}
