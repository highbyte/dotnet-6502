using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Impl.SilkNet.Generic.Input;

public class GenericComputerSilkNetInputHandler : IInputHandler
{
    private readonly GenericComputer _genericComputer;
    public ISystem System => _genericComputer;
    private readonly SilkNetInputHandlerContext _inputHandlerContext;
    private readonly EmulatorInputConfig _emulatorInputConfig;
    public List<string> GetDebugInfo() => new();

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();


    public GenericComputerSilkNetInputHandler(GenericComputer genericComputer, SilkNetInputHandlerContext inputHandlerContext, EmulatorInputConfig emulatorInputConfig)
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
        // TODO: Generating random number to send to generic computer should not be in input handler, because it'll not run if not in focus.
        CaptureRandomNumber(_genericComputer);
    }

    public void Cleanup()
    {
    }

    private void CaptureKeyboard(GenericComputer genericComputer)
    {
        // Note: The simplistic "GenericComputer" don't have a Keyboard buffer, only can receive one character ...
        if (_inputHandlerContext!.KeysDown.Count > 0)
        {
            var keyDown = _inputHandlerContext.KeysDown.First();

            byte genericKeyByteValue;
            if (!GenericComputerSilkNetKeyboard.SilkNetToGenericKeyMap.ContainsKey(keyDown))
                genericKeyByteValue = (byte)keyDown;
            else
                genericKeyByteValue = (byte)GenericComputerSilkNetKeyboard.SilkNetToGenericKeyMap[keyDown];

            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = genericKeyByteValue;
        }
        else
        {
            // Only way to tell the "GenericComputer" that a Key is no longer pressed is to set KeyDownAddress to 0...
            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = 0x00;
        }
    }

    private void CaptureRandomNumber(GenericComputer system)
    {
        var emulatorMem = system.Mem;

        var rnd = (byte)new Random().Next(0, 255);
        emulatorMem[_emulatorInputConfig.RandomValueAddress] = rnd;
    }

}
