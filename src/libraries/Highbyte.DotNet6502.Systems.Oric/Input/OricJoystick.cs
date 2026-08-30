using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Oric.Input;

/// <summary>
/// Two Atari-style joystick sockets exposed by a PASE or IJK printer-port adapter.
/// </summary>
public sealed class OricJoystick
{
    private const byte JoystickSelectMask = 0xc0;
    private const byte IjkEnableMask = 0x10;
    private const byte IjkPresentMask = 0x20;

    private readonly Dictionary<int, HashSet<JoystickAction>> _currentActions = new()
    {
        [1] = [],
        [2] = [],
    };

    public OricJoystickInterface Interface { get; set; }
    public bool KeyboardJoystickEnabled { get; set; }
    public int KeyboardJoystick { get; set; }

    public IReadOnlyDictionary<int, HashSet<JoystickAction>> CurrentJoystickActions => _currentActions;

    public OricJoystick(OricConfig config)
    {
        Interface = config.JoystickInterface;
        KeyboardJoystickEnabled = config.KeyboardJoystickEnabled;
        KeyboardJoystick = config.KeyboardJoystick;
    }

    public void ClearJoystickActions()
    {
        _currentActions[1].Clear();
        _currentActions[2].Clear();
    }

    public void SetJoystickActions(int joystick, IEnumerable<JoystickAction> actions, bool overwrite = true)
    {
        ValidateJoystickNumber(joystick);
        if (overwrite)
            _currentActions[joystick].Clear();

        foreach (var action in actions)
        {
            if (action == JoystickAction.Fire2)
                continue;
            _currentActions[joystick].Add(action);
        }
    }

    /// <summary>Returns the active-low value driven onto the VIA Port A input lines.</summary>
    public byte ReadPortAInput(byte portAOutput, byte ddrA, byte portBOutput, byte ddrB)
        => Interface switch
        {
            OricJoystickInterface.PASE => ReadPase(portAOutput, ddrA),
            OricJoystickInterface.IJK => ReadIjk(portAOutput, ddrA, portBOutput, ddrB),
            _ => 0xff,
        };

    private byte ReadPase(byte portAOutput, byte ddrA)
    {
        if ((ddrA & JoystickSelectMask) != JoystickSelectMask)
            return 0xff;

        var joystick = (portAOutput & JoystickSelectMask) switch
        {
            0x80 => 1,
            0x40 => 2,
            _ => 0,
        };
        if (joystick == 0)
            return 0xff;

        var value = (byte)0xff;
        foreach (var action in _currentActions[joystick])
            value &= (byte)~GetPasePortMask(action);
        return value;
    }

    private byte ReadIjk(byte portAOutput, byte ddrA, byte portBOutput, byte ddrB)
    {
        var interfaceEnabled = (ddrB & IjkEnableMask) != 0 && (portBOutput & IjkEnableMask) == 0;
        if (!interfaceEnabled || (ddrA & JoystickSelectMask) != JoystickSelectMask)
            return 0xff;

        var value = (byte)(0xff & ~IjkPresentMask);
        var joystick = (portAOutput & JoystickSelectMask) switch
        {
            0x40 => 1,
            0x80 => 2,
            _ => 0,
        };
        if (joystick == 0)
            return value;

        foreach (var action in _currentActions[joystick])
            value &= (byte)~GetIjkPortMask(action);
        return value;
    }

    private static byte GetPasePortMask(JoystickAction action) => action switch
    {
        JoystickAction.Left => 0x01,
        JoystickAction.Right => 0x02,
        JoystickAction.Down => 0x08,
        JoystickAction.Up => 0x10,
        JoystickAction.Fire => 0x20,
        _ => 0,
    };

    private static byte GetIjkPortMask(JoystickAction action) => action switch
    {
        JoystickAction.Right => 0x01,
        JoystickAction.Left => 0x02,
        JoystickAction.Fire => 0x04,
        JoystickAction.Down => 0x08,
        JoystickAction.Up => 0x10,
        _ => 0,
    };

    private static void ValidateJoystickNumber(int joystick)
    {
        if (joystick is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(joystick), joystick, "Oric joystick must be 1 or 2.");
    }
}
