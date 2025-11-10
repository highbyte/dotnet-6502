using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Avalonia.Generic.Input;

public class AvaloniaGenericInputHandler : IInputHandler
{
    private readonly GenericComputer _genericComputer;
    private readonly AvaloniaInputHandlerContext _inputHandlerContext;
    private readonly EmulatorInputConfig _emulatorInputConfig;
    private readonly ILogger _logger;

    public Instrumentations Instrumentations { get; } = new();
    public ISystem System => _genericComputer;

    public List<string> GetDebugInfo() => new();

    public AvaloniaGenericInputHandler(
        GenericComputer genericComputer,
        AvaloniaInputHandlerContext inputHandlerContext,
        EmulatorInputConfig emulatorInputConfig,
        ILoggerFactory loggerFactory)
    {
        _genericComputer = genericComputer;
        _inputHandlerContext = inputHandlerContext;
        _emulatorInputConfig = emulatorInputConfig;
        _logger = loggerFactory.CreateLogger(typeof(AvaloniaGenericInputHandler).Name);
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
        // Cleanup input handling if needed
    }

    private void CaptureKeyboard(GenericComputer genericComputer)
    {
        // Note: The simplistic "GenericComputer" don't have a Keyboard buffer, only can receive one character ...
        if (_inputHandlerContext!.KeysDown.Count > 0)
        {
            Key key = _inputHandlerContext.KeysDown.First();
            // TODO: Handle all kinds of keys
            if (GenericComputerAvaloniaKeyboard.AvaloniaToGenericKeyMap.ContainsKey(key))
            {
                byte keyCode = (byte)GenericComputerAvaloniaKeyboard.AvaloniaToGenericKeyMap[key];
                genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = keyCode;
            }

        }

        if (_inputHandlerContext.KeysDown.Count == 0)
            // Only way to tell the "GenericComputer" that a Key is no longer pressed is to set KeyDownAddress to 0...
            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = 0x00;
    }

    private void CaptureRandomNumber(GenericComputer system)
    {
        var emulatorMem = system.Mem;

        var rnd = (byte)new Random().Next(0, 255);
        emulatorMem[_emulatorInputConfig.RandomValueAddress] = rnd;
    }
}
