using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;

public class C64AspNetInputConfig : ICloneable
{
    public int CurrentJoystick = 2;

    public List<int> AvailableJoysticks = new() { 1, 2 };

    public Dictionary<int, Dictionary<int[], C64JoystickAction[]>> GamePadToC64JoystickMap = new()
    {
        {
            1,
            new Dictionary<int[], C64JoystickAction[]>
            {
                { new[] { 0 }, new[] { C64JoystickAction.Fire } },
                { new[] { 12 }, new[] { C64JoystickAction.Up} },
                { new[] { 13 }, new[] { C64JoystickAction.Down } },
                { new[] { 14 }, new[] { C64JoystickAction.Left } },
                { new[] { 15 }, new[] { C64JoystickAction.Right } },
            }
        },
        {
            2,
            new Dictionary<int[], C64JoystickAction[]>
            {
                { new[] { 0 }, new[] { C64JoystickAction.Fire } },
                { new[] { 12 }, new[] { C64JoystickAction.Up} },
                { new[] { 13 }, new[] { C64JoystickAction.Down } },
                { new[] { 14 }, new[] { C64JoystickAction.Left } },
                { new[] { 15 }, new[] { C64JoystickAction.Right } },
            }
        }
    };

    public object Clone()
    {
        return MemberwiseClone();
    }
}
