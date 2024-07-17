using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SadConsole.Generic.Input;

public class GenericSadConsoleInputHandler : IInputHandler<GenericComputer, SadConsoleInputHandlerContext>
{
    private GenericComputer _genericComputer;
    private SadConsoleInputHandlerContext? _inputHandlerContext;

    private readonly EmulatorInputConfig _emulatorInputConfig;
    private readonly ILoggerFactory _loggerFactory;

    public List<string> GetDebugInfo() => new();

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();


    public GenericSadConsoleInputHandler(EmulatorInputConfig emulatorInputConfig, ILoggerFactory loggerFactory)
    {
        _emulatorInputConfig = emulatorInputConfig;
        _loggerFactory = loggerFactory;
    }

    public void Init(GenericComputer genericComputer, SadConsoleInputHandlerContext inputHandlerContext)
    {
        _genericComputer = genericComputer;
        _inputHandlerContext = inputHandlerContext;
        _inputHandlerContext.Init();
    }

    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
        Init((GenericComputer)system, (SadConsoleInputHandlerContext)inputHandlerContext);
    }

    public void BeforeFrame()
    {
        CaptureKeyboard(_genericComputer);
        CaptureRandomNumber(_genericComputer);
    }

    public void BeforeFrame(ISystem system)
    {
        BeforeFrame((GenericComputer)system);
    }

    private void CaptureKeyboard(GenericComputer system)
    {
        var emulatorMem = system.Mem;
        var keyboard = GameHost.Instance.Keyboard;

        if (keyboard.KeysPressed.Count > 0)
            emulatorMem[_emulatorInputConfig.KeyPressedAddress] = (byte)keyboard.KeysPressed[0].Character;
        else
            emulatorMem[_emulatorInputConfig.KeyPressedAddress] = 0x00;

        if (keyboard.KeysDown.Count > 0)
            emulatorMem[_emulatorInputConfig.KeyDownAddress] = (byte)keyboard.KeysDown[0].Character;
        else
            emulatorMem[_emulatorInputConfig.KeyDownAddress] = 0x00;

        if (keyboard.KeysReleased.Count > 0)
            emulatorMem[_emulatorInputConfig.KeyReleasedAddress] = (byte)keyboard.KeysReleased[0].Character;
        else
            emulatorMem[_emulatorInputConfig.KeyReleasedAddress] = 0x00;
    }

    private void CaptureRandomNumber(GenericComputer system)
    {
        var emulatorMem = system.Mem;

        var rnd = (byte)new Random().Next(0, 255);
        emulatorMem[_emulatorInputConfig.RandomValueAddress] = rnd;
    }
}
