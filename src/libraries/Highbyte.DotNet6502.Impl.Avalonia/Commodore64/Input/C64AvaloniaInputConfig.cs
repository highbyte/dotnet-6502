using Avalonia.Input;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Impl.Avalonia.Commodore64.Input;

public class C64AvaloniaInputConfig : ICloneable
{
    public int CurrentJoystick = 2;

    public List<int> AvailableJoysticks = new() { 1, 2 };

    // Map Avalonia keys to C64 joystick actions
    public Dictionary<int, Dictionary<Key, C64JoystickAction>> KeyToC64JoystickMap = new()
    {
        {
            1,
            new Dictionary<Key, C64JoystickAction>
            {
                { Key.Space, C64JoystickAction.Fire },
                { Key.Up, C64JoystickAction.Up },
                { Key.Down, C64JoystickAction.Down },
                { Key.Left, C64JoystickAction.Left },
                { Key.Right, C64JoystickAction.Right },
            }
        },
        {
            2,
            new Dictionary<Key, C64JoystickAction>
            {
                { Key.LeftCtrl, C64JoystickAction.Fire },
                { Key.W, C64JoystickAction.Up },
                { Key.S, C64JoystickAction.Down },
                { Key.A, C64JoystickAction.Left },
                { Key.D, C64JoystickAction.Right },
            }
        }
    };

    // Map gamepad buttons to C64 joystick actions
    public Dictionary<int, Dictionary<GamepadButton[], C64JoystickAction[]>> GamepadToC64JoystickMap = new()
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
        var clone = new C64AvaloniaInputConfig
        {
            CurrentJoystick = CurrentJoystick,
            AvailableJoysticks = new List<int>(AvailableJoysticks),
            KeyToC64JoystickMap = new Dictionary<int, Dictionary<Key, C64JoystickAction>>(),
            GamepadToC64JoystickMap = new Dictionary<int, Dictionary<GamepadButton[], C64JoystickAction[]>>()
        };

        foreach (var joystickMap in KeyToC64JoystickMap)
        {
            clone.KeyToC64JoystickMap[joystickMap.Key] = new Dictionary<Key, C64JoystickAction>(joystickMap.Value);
        }

        foreach (var joystickMap in GamepadToC64JoystickMap)
        {
            clone.GamepadToC64JoystickMap[joystickMap.Key] = new Dictionary<GamepadButton[], C64JoystickAction[]>(joystickMap.Value);
        }

        return clone;
    }
}
