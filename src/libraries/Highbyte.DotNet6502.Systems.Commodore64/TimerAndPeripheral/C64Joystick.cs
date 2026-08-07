using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public class C64Joystick
{
    private readonly ILogger _logger;
    public Dictionary<int, HashSet<JoystickAction>> CurrentJoystickActions { get; private set; } = new()
    {
        {1, new() },
        {2, new() }
    };

    public bool KeyboardJoystickEnabled { get; set; }
    public int KeyboardJoystick { get; set; } = 2;
    public C64KeyboardJoystickMap KeyboardJoystickMap { get; private set; }

    public C64Joystick(C64Config c64Config, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(C64Joystick));
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

    public void SetJoystickActions(int joystick, HashSet<JoystickAction> joystickActions, bool overwrite = true)
    {
        if (joystick != 1 && joystick != 2)
            throw new ArgumentException($"Joystick number {joystick} is not supported. Valid values are 1 and 2.");

        if (joystickActions.Count > 0 && _logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("C64 joystick {Joystick} pressed: {Actions}", joystick, string.Join(",", joystickActions));

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

    /// <summary>
    /// The CIA 1 data-port bit a joystick action pulls low. Stated here rather than cast from the
    /// enum's numeric value: <see cref="JoystickAction"/> is shared with other systems, and a
    /// reorder or a new member added for one of them would otherwise silently change which bit
    /// the C64 clears.
    /// </summary>
    public static int GetPortBit(JoystickAction action) => action switch
    {
        JoystickAction.Up => 0,
        JoystickAction.Down => 1,
        JoystickAction.Left => 2,
        JoystickAction.Right => 3,
        JoystickAction.Fire => 4,
        _ => throw new ArgumentOutOfRangeException(
            nameof(action), action, "No C64 joystick port bit for this action."),
    };
}
