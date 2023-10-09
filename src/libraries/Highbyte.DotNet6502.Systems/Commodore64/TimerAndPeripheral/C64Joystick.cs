using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public class C64Joystick
{
    public bool KeyboardJoystickEnabled { get; set; }
    public C64KeyboardJoystickMap KeyboardJoystickMap { get; private set; }
    public HashSet<C64JoystickAction> CurrentJoystick1Actions { get; private set; } = new();
    public HashSet<C64JoystickAction> CurrentJoystick2Actions { get; private set; } = new();

    public C64Joystick(C64Config c64Config)
    {
        KeyboardJoystickEnabled = c64Config.KeyboardJoystickEnabled;
        KeyboardJoystickMap = c64Config.KeyboardJoystickMap;
    }

    public void SetJoystick1Actions(HashSet<C64JoystickAction> joystickActions)
    {
        CurrentJoystick1Actions = joystickActions;
    }

    public void SetJoystick2Actions(HashSet<C64JoystickAction> joystickActions)
    {
        CurrentJoystick2Actions = joystickActions;
    }

    /// <summary>
    /// Possible joystick actions. More than one can be active at the same time.
    /// The integer value corresponds to the bit position in the joystick register (set = not active, clear = active).
    /// </summary>
    public enum C64JoystickAction
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
        Fire = 4
    }
}
