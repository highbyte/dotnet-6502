using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64
{
    public class C64SilkNetConfig : ICloneable
    {
        public int CurrentJoystick = 2;

        public List<int> AvailableJoysticks = new() { 1, 2 };

        public Dictionary<int, Dictionary<ButtonName[], C64JoystickAction[]>> GamePadToC64JoystickMap = new()
        {
            {
                1,
                new Dictionary<ButtonName[], C64JoystickAction[]>
                {
                    { new[] { ButtonName.A }, new[] { C64JoystickAction.Fire } },
                    { new[] { ButtonName.DPadUp }, new[] { C64JoystickAction.Up} },
                    { new[] { ButtonName.DPadDown }, new[] { C64JoystickAction.Down } },
                    { new[] { ButtonName.DPadLeft }, new[] { C64JoystickAction.Left } },
                    { new[] { ButtonName.DPadRight }, new[] { C64JoystickAction.Right } },
                }
            },
            {
                2,
                new Dictionary<ButtonName[], C64JoystickAction[]>
                {
                    { new[] { ButtonName.A }, new[] { C64JoystickAction.Fire } },
                    { new[] { ButtonName.DPadUp }, new[] { C64JoystickAction.Up} },
                    { new[] { ButtonName.DPadDown }, new[] { C64JoystickAction.Down } },
                    { new[] { ButtonName.DPadLeft }, new[] { C64JoystickAction.Left } },
                    { new[] { ButtonName.DPadRight }, new[] { C64JoystickAction.Right } },
                }
            }
        };

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
