using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public partial class C64Joystick
{
    private static readonly JoystickAction[] PortActions =
        [JoystickAction.Up, JoystickAction.Down, JoystickAction.Left, JoystickAction.Right, JoystickAction.Fire];

    internal byte CapturePort(int port)
    {
        byte pressed = 0;
        foreach (var action in CurrentJoystickActions[port])
            pressed |= (byte)(1 << GetPortBit(action));
        return pressed;
    }

    internal void RestorePort(int port, byte pressed)
    {
        var actions = CurrentJoystickActions[port];
        actions.Clear();
        foreach (var action in PortActions)
            if ((pressed & (1 << GetPortBit(action))) != 0)
                actions.Add(action);
    }
}
