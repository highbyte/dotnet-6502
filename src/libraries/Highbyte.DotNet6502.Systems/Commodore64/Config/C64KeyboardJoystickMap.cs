using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.C64Joystick;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config;

public class C64KeyboardJoystickMap
{
    public Dictionary<char, C64JoystickAction> KeyToJoystick1Map = new()
    {
    };
    public Dictionary<char, C64JoystickAction> KeyToJoystick2Map = new()
    {
            {'W', C64JoystickAction.Up},
            {'S', C64JoystickAction.Down},
            {'A', C64JoystickAction.Left},
            {'D', C64JoystickAction.Right},
            {' ', C64JoystickAction.Fire},
    };

    public List<char> GetMappedKeysForJoystickAction(int joystick, C64JoystickAction action)
    {
        if (joystick != 1 && joystick != 2)
            throw new ArgumentException($"Invalid joystick number: {joystick}");
        var map = joystick == 1 ? KeyToJoystick1Map : KeyToJoystick2Map;
        return map.Where(x => x.Value == action).Select(x => x.Key).ToList();
    }
}
