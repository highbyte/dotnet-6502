using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.AspNet.Generic.Input;

public class GenericComputerAspNetInputHandler : IInputHandler<GenericComputer, AspNetInputHandlerContext>
{
    private readonly EmulatorInputConfig _emulatorInputConfig;
    private AspNetInputHandlerContext? _inputHandlerContext;

    public List<string> GetDebugInfo() => new();

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();

    public GenericComputerAspNetInputHandler(EmulatorInputConfig emulatorInputConfig)
    {
        _emulatorInputConfig = emulatorInputConfig;
    }

    public void Init(GenericComputer system, AspNetInputHandlerContext inputHandlerContext)
    {
        _inputHandlerContext = inputHandlerContext;
        _inputHandlerContext.Init();
    }

    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
        Init((GenericComputer)system, (AspNetInputHandlerContext)inputHandlerContext);
    }

    public void ProcessInput(GenericComputer genericComputer)
    {
        CaptureKeyboard(genericComputer);
    }

    public void ProcessInput(ISystem system)
    {
        ProcessInput((GenericComputer)system);
    }

    private void CaptureKeyboard(GenericComputer genericComputer)
    {
        // Note: The simplistic "GenericComputer" don't have a Keyboard buffer, only can receive one character ...
        if (_inputHandlerContext!.KeysDown.Count > 0)
        {
            var key = _inputHandlerContext.KeysDown.First();
            // TODO: Handle all kinds of keys
            byte keyCode = 0;
            if (key.Length == 1)
                keyCode = (byte)key[0];
            else if (key == "Space")
                keyCode = 32;
            else if (key == "Enter")
                keyCode = 10;
            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = keyCode;
        }

        if (_inputHandlerContext.KeysDown.Count == 0)
            // Only way to tell the "GenericComputer" that a Key is no longer pressed is to set KeyDownAddress to 0...
            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = 0x00;
    }
}
