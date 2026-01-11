using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Impl.AspNet.Generic.Input;

public class GenericComputerAspNetInputHandler : IInputHandler
{
    private readonly GenericComputer _genericComputer;
    public ISystem System => _genericComputer;
    private readonly EmulatorInputConfig _emulatorInputConfig;
    private readonly AspNetInputHandlerContext _inputHandlerContext;

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
            var key = _inputHandlerContext.KeysDown.First();
            // TODO: Handle all kinds of keys
            byte keyCode = 0;
            if (key.Length == 1)
                keyCode = (byte)key[0];
            else
                keyCode = (byte)GenericComputerAspNetKeyboard.AspNetToGenericKeyMap[key];

            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = keyCode;
        }

        if (_inputHandlerContext.KeysDown.Count == 0)
            // Only way to tell the "GenericComputer" that a Key is no longer pressed is to set KeyDownAddress to 0...
            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = 0x00;
    }

    public List<string> GetDebugInfo()
    {
        List<string> list = new();
        if (_inputHandlerContext == null)
            return list;

        if (_inputHandlerContext.KeysDown.Count > 0)
            list.Add($"KeysDown: {string.Join(',', _inputHandlerContext.KeysDown)}");
        if (_inputHandlerContext.GamepadButtonsDown.Count > 0)
            list.Add($"GamepadDown: {string.Join(',', _inputHandlerContext.GamepadButtonsDown)}");
        return list;
    }


    private void CaptureRandomNumber(GenericComputer system)
    {
        var emulatorMem = system.Mem;

        var rnd = (byte)new Random().Next(0, 255);
        emulatorMem[_emulatorInputConfig.RandomValueAddress] = rnd;
    }
}
