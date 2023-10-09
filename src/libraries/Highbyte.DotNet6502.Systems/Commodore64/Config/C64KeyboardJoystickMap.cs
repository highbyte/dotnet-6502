using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using static Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.C64Joystick;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config;

public class C64KeyboardJoystickMap
{
    public Dictionary<C64Key, C64JoystickAction> KeyToJoystick1Map = new()
    {
    };
    public Dictionary<C64Key, C64JoystickAction> KeyToJoystick2Map = new()
    {
            {C64Key.W, C64JoystickAction.Up},
            {C64Key.S, C64JoystickAction.Down},
            {C64Key.A, C64JoystickAction.Left},
            {C64Key.D, C64JoystickAction.Right},
            {C64Key.Space, C64JoystickAction.Fire},
    };

    public List<C64Key> GetMappedKeysForJoystickAction(int joystick, C64JoystickAction action)
    {
        if (joystick != 1 && joystick != 2)
            throw new ArgumentException($"Invalid joystick number: {joystick}");
        var map = joystick == 1 ? KeyToJoystick1Map : KeyToJoystick2Map;
        return map.Where(x => x.Value == action).Select(x => x.Key).ToList();
    }
}
