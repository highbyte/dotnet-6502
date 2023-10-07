using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.C64Joystick;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;

public class C64AspNetInputHandler : IInputHandler<C64, AspNetInputHandlerContext>
{
    private AspNetInputHandlerContext? _inputHandlerContext = default!;

    public C64AspNetInputHandler()
    {
    }

    public void Init(C64 system, AspNetInputHandlerContext inputHandlerContext)
    {
        _inputHandlerContext = inputHandlerContext;
        _inputHandlerContext.Init();
    }

    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
        Init((C64)system, (AspNetInputHandlerContext)inputHandlerContext);
    }

    public void ProcessInput(C64 c64)
    {
        CaptureKeyboard(c64);

        CaptureJoystick(c64);
    }

    public void ProcessInput(ISystem system)
    {
        ProcessInput((C64)system);
    }

    private void CaptureKeyboard(C64 c64)
    {
        var keyboard = c64.Cia.Keyboard;
        // TODO after implementing C64 keyboard matrix scanning
    }

    private void CaptureJoystick(C64 c64)
    {
        var joystick = c64.Cia.Joystick;

        // Use keypresses as joystick input for now.
        if (joystick.KeyboardJoystickEnabled)
        {
            var joystick1KeyboardMap = joystick.KeyboardJoystickMap.KeyToJoystick1Map;
            var joystick1Actions = new HashSet<C64JoystickAction>();
            foreach (var charCode in joystick1KeyboardMap.Keys)
            {
                string key = charCode.ToString().ToLower();
                if (_inputHandlerContext!.KeysDown.Contains(key))
                    joystick1Actions.Add(joystick1KeyboardMap[charCode]);
            }
            c64.Cia.Joystick.SetJoystick1Actions(joystick1Actions);

            var joystick2KeyboardMap = joystick.KeyboardJoystickMap.KeyToJoystick2Map;
            var joystick2Actions = new HashSet<C64JoystickAction>();
            foreach (var charCode in joystick2KeyboardMap.Keys)
            {
                string key = charCode.ToString().ToLower();
                if (_inputHandlerContext!.KeysDown.Contains(key))
                    joystick2Actions.Add(joystick2KeyboardMap[charCode]);
            }
            c64.Cia.Joystick.SetJoystick2Actions(joystick2Actions);
        }
    }

    public List<string> GetStats()
    {
        List<string> list = new();
        if (_inputHandlerContext == null)
            return list;

        if (_inputHandlerContext.KeysDown.Count > 0)
            list.Add($"KeysDown: {string.Join(',', _inputHandlerContext.KeysDown)}");
        return list;
    }
}
