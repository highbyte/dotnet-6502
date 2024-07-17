using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.SilkNet.Generic.Input;

public class GenericComputerSilkNetInputHandler : IInputHandler<GenericComputer, SilkNetInputHandlerContext>
{
    private readonly EmulatorInputConfig _emulatorInputConfig;
    private GenericComputer _genericComputer;
    private SilkNetInputHandlerContext? _inputHandlerContext;
    public List<string> GetDebugInfo() => new();

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();

    public GenericComputerSilkNetInputHandler(EmulatorInputConfig emulatorInputConfig)
    {
        _emulatorInputConfig = emulatorInputConfig;
    }

    public void Init(GenericComputer genericComputer, SilkNetInputHandlerContext inputHandlerContext)
    {
        _genericComputer = genericComputer;
        _inputHandlerContext = inputHandlerContext;
        _inputHandlerContext.Init();
    }

    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
        Init((GenericComputer)system, (SilkNetInputHandlerContext)inputHandlerContext);
    }

    public void BeforeFrame()
    {
        CaptureKeyboard(_genericComputer);
    }

    public void BeforeFrame(ISystem system)
    {
        BeforeFrame((GenericComputer)system);
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
}
