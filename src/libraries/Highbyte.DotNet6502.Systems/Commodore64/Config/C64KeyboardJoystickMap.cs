using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config;

public class C64KeyboardJoystickMap
{
    private Dictionary<C64Key, C64JoystickAction> KeyToJoystick1Map = new()
    {
    };

    private Dictionary<C64Key, C64JoystickAction> KeyToJoystick2Map = new()
    {
            {C64Key.Space, C64JoystickAction.Fire},
            {C64Key.W, C64JoystickAction.Up},
            {C64Key.S, C64JoystickAction.Down},
            {C64Key.A, C64JoystickAction.Left},
            {C64Key.D, C64JoystickAction.Right}
    };

    public Dictionary<C64Key, C64JoystickAction> GetMap(int joystick)
    {
        if (joystick != 1 && joystick != 2)
            throw new ArgumentException($"Invalid joystick number: {joystick}");
        return joystick == 1 ? KeyToJoystick1Map : KeyToJoystick2Map;
    }
}
