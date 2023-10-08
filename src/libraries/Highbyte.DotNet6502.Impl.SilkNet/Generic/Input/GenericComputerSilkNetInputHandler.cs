using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.SilkNet.Generic.Input;

public class GenericComputerSilkNetInputHandler : IInputHandler<GenericComputer, SilkNetInputHandlerContext>
{
    private readonly EmulatorInputConfig _emulatorInputConfig;
    private SilkNetInputHandlerContext? _inputHandlerContext;
    private readonly List<string> _stats = new();

    public GenericComputerSilkNetInputHandler(EmulatorInputConfig emulatorInputConfig)
    {
        _emulatorInputConfig = emulatorInputConfig;
    }

    public void Init(GenericComputer system, SilkNetInputHandlerContext inputHandlerContext)
    {
        _inputHandlerContext = inputHandlerContext;
        _inputHandlerContext.Init();
    }

    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
        Init((GenericComputer)system, (SilkNetInputHandlerContext)inputHandlerContext);
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
            var keyDown = _inputHandlerContext.KeysDown.First();
            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = (byte)keyDown;
        }
        else
        {
            // Only way to tell the "GenericComputer" that a Key is no longer pressed is to set KeyDownAddress to 0...
            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = 0x00;
        }
    }

    public List<string> GetStats()
    {
        return _stats;
    }
}
