using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public class C64Joystick
{
    private readonly ILogger<C64Joystick> _logger;
    public Dictionary<int, HashSet<C64JoystickAction>> CurrentJoystickActions { get; private set; } = new()
    {
        {1, new() },
        {2, new() }
    };

    public bool KeyboardJoystickEnabled { get; set; }
    public int KeyboardJoystick { get; set; } = 2;
    public C64KeyboardJoystickMap KeyboardJoystickMap { get; private set; }

    public C64Joystick(C64Config c64Config, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<C64Joystick>();
        KeyboardJoystickEnabled = c64Config.KeyboardJoystickEnabled;
        KeyboardJoystick = c64Config.KeyboardJoystick;
        KeyboardJoystickMap = c64Config.KeyboardJoystickMap;
    }

    public void ClearJoystickActions()
    {
        for (int joystick = 1; joystick <= CurrentJoystickActions.Count; joystick++)
        {
            CurrentJoystickActions[joystick].Clear();
        }
    }

    public void SetJoystickActions(int joystick, HashSet<C64JoystickAction> joystickActions, bool overwrite = true)
    {
        if (joystick != 1 && joystick != 2)
            throw new ArgumentException($"Joystick number {joystick} is not supported. Valid values are 1 and 2.");

        if (joystickActions.Count > 0)
            _logger.LogDebug($"C64 joystick {joystick} pressed: {string.Join(",", joystickActions)}");

        if (overwrite)
        {
            CurrentJoystickActions[joystick] = joystickActions;
        }
        else
        {
            foreach (var action in joystickActions)
            {
                if (!CurrentJoystickActions[joystick].Contains(action))
                    CurrentJoystickActions[joystick].Add(action);
            }
        }
    }
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
