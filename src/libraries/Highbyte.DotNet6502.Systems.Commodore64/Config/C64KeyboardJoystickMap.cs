using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config;

public class C64KeyboardJoystickMap
{
    private Dictionary<C64Key, JoystickAction> KeyToJoystick1Map = new()
    {
            {C64Key.Space, JoystickAction.Fire},
            {C64Key.W, JoystickAction.Up},
            {C64Key.S, JoystickAction.Down},
            {C64Key.A, JoystickAction.Left},
            {C64Key.D, JoystickAction.Right}
    };

    private Dictionary<C64Key, JoystickAction> KeyToJoystick2Map = new()
    {
            {C64Key.Space, JoystickAction.Fire},
            {C64Key.W, JoystickAction.Up},
            {C64Key.S, JoystickAction.Down},
            {C64Key.A, JoystickAction.Left},
            {C64Key.D, JoystickAction.Right}
    };

    public Dictionary<C64Key, JoystickAction> GetMap(int joystick)
    {
        if (joystick != 1 && joystick != 2)
            throw new ArgumentException($"Invalid joystick number: {joystick}");
        return joystick == 1 ? KeyToJoystick1Map : KeyToJoystick2Map;
    }
}
