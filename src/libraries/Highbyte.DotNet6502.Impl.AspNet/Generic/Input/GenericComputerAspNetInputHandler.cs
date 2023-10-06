using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.AspNet.Generic.Input;

public class GenericComputerAspNetInputHandler : IInputHandler<GenericComputer, AspNetInputHandlerContext>
{
    private readonly EmulatorInputConfig _emulatorInputConfig;
    private AspNetInputHandlerContext? _inputHandlerContext;
    private readonly List<string> _stats = new();

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

        _inputHandlerContext!.ClearKeys();   // Clear our captured keys so far
    }

    public void ProcessInput(ISystem system)
    {
        ProcessInput((GenericComputer)system);
    }

    private void CaptureKeyboard(GenericComputer genericComputer)
    {
        // Note: The simplistic "GenericComputer" don't have a Keyboard buffer, only can receive one character ...

        // if (_inputHandlerContext.CharactersReceived.Count > 0)
        // {
        //     char keyCode = _inputHandlerContext.CharactersReceived.First();
        //     genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = (byte)keyCode;
        // }

        if (_inputHandlerContext!.KeysDown.Count > 0)
        {
            var key = _inputHandlerContext.KeysDown.First();
            // TODO: Handle all kinds of keys
            var keyCode = 0;
            if (key.Length == 1)
                keyCode = key[0];
            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = (byte)keyCode;
        }

        if (_inputHandlerContext.KeysUp.Count > 0)
            // Only way to tell the "GenericComputer" that a Key is no longer pressed is to set KeyDownAddress to 0...
            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = 0x00;
    }

    public List<string> GetStats()
    {
        return _stats;
    }
}
