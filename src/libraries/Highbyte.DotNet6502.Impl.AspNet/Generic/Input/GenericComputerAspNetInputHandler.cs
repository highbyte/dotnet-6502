using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.AspNet.Generic.Input;

public class GenericComputerAspNetInputHandler : IInputHandler
{
    private readonly GenericComputer _genericComputer;
    public ISystem System => _genericComputer;
    private readonly EmulatorInputConfig _emulatorInputConfig;
    private readonly AspNetInputHandlerContext _inputHandlerContext;

    public List<string> GetDebugInfo() => new();

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();


    public GenericComputerAspNetInputHandler(GenericComputer genericComputer, AspNetInputHandlerContext inputHandlerContext, EmulatorInputConfig emulatorInputConfig)
    {
        _genericComputer = genericComputer;
        _inputHandlerContext = inputHandlerContext;
        _emulatorInputConfig = emulatorInputConfig;
    }

    public void Init()
    {
    }


    public void BeforeFrame()
    {
        CaptureKeyboard(_genericComputer);
    }

    public void Cleanup()
    {
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
