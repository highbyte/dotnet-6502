using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.SadConsole.Generic;

public class GenericSadConsoleInputHandler : IInputHandler<GenericComputer, SadConsoleInputHandlerContext>, IInputHandler
{
    private SadConsoleInputHandlerContext? _inputHandlerContext;

    private readonly EmulatorInputConfig _emulatorInputConfig;

    public GenericSadConsoleInputHandler(
        EmulatorInputConfig emulatorInputConfig)
    {
        _emulatorInputConfig = emulatorInputConfig;
    }

    public void Init(GenericComputer system, SadConsoleInputHandlerContext inputHandlerContext)
    {
        _inputHandlerContext = inputHandlerContext;
        //_inputHandlerContext.Init();
    }

    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
        Init((GenericComputer)system, (SadConsoleInputHandlerContext)inputHandlerContext);
    }

    public void ProcessInput(GenericComputer system)
    {
        CaptureKeyboard(system);
        CaptureRandomNumber(system);
    }

    public void ProcessInput(ISystem system)
    {
        ProcessInput((GenericComputer)system);
    }

    private void CaptureKeyboard(GenericComputer system)
    {
        var emulatorMem = system.Mem;
        var keyboard = GameHost.Instance.Keyboard;

        if(keyboard.KeysPressed.Count > 0)
            emulatorMem[_emulatorInputConfig.KeyPressedAddress] = (byte)keyboard.KeysPressed[0].Character;
        else
            emulatorMem[_emulatorInputConfig.KeyPressedAddress] = 0x00;

        if(keyboard.KeysDown.Count > 0)
            emulatorMem[_emulatorInputConfig.KeyDownAddress] = (byte)keyboard.KeysDown[0].Character;
        else
            emulatorMem[_emulatorInputConfig.KeyDownAddress] = 0x00;

        if(keyboard.KeysReleased.Count > 0)
            emulatorMem[_emulatorInputConfig.KeyReleasedAddress] = (byte)keyboard.KeysReleased[0].Character;
        else
            emulatorMem[_emulatorInputConfig.KeyReleasedAddress] = 0x00;
    }

    private void CaptureRandomNumber(GenericComputer system)
    {
        var emulatorMem = system.Mem;

        byte rnd = (byte)new Random().Next(0, 255);
        emulatorMem[_emulatorInputConfig.RandomValueAddress] = rnd;
    }
    public string GetDebugMessage()
    {
        return "";
    }
}
