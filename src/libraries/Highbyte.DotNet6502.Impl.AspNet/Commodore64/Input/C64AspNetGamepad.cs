using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;

public static class C64AspNetGamepad
{
    public static Dictionary<int[], C64JoystickAction[]> AspNetGamePadToC64JoystickMap = new()
    {
        { new[] { 0 }, new[] { C64JoystickAction.Fire } },
        { new[] { 12 }, new[] { C64JoystickAction.Up} },
        { new[] { 13 }, new[] { C64JoystickAction.Down } },
        { new[] { 14 }, new[] { C64JoystickAction.Left } },
        { new[] { 15 }, new[] { C64JoystickAction.Right } },
    };
}
