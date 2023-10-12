using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input;

public static class C64SilkNetGamepad
{

    public static Dictionary<ButtonName[], C64JoystickAction[]> SilkNetGamePadToC64JoystickMap = new()
    {
        { new[] { ButtonName.A }, new[] { C64JoystickAction.Fire } },
        { new[] { ButtonName.DPadUp }, new[] { C64JoystickAction.Up} },
        { new[] { ButtonName.DPadDown }, new[] { C64JoystickAction.Down } },
        { new[] { ButtonName.DPadLeft }, new[] { C64JoystickAction.Left } },
        { new[] { ButtonName.DPadRight }, new[] { C64JoystickAction.Right } },
    };
}
