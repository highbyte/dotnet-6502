using System;
using System.Collections.Generic;
using System.Linq;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Avalonia.Generic.Input;

public class AvaloniaGenericInputHandler : IInputConsumer
{
    private readonly GenericComputer _genericComputer;
    private IHostInputState _hostInput = default!;
    private readonly EmulatorInputConfig _emulatorInputConfig;
    private readonly ILogger _logger;

    public Instrumentations Instrumentations { get; } = new();
    public ISystem System => _genericComputer;

    public List<string> GetDebugInfo() => new();

    public AvaloniaGenericInputHandler(
        GenericComputer genericComputer,
        EmulatorInputConfig emulatorInputConfig,
        ILoggerFactory loggerFactory)
    {
        _genericComputer = genericComputer;
        _emulatorInputConfig = emulatorInputConfig;
        _logger = loggerFactory.CreateLogger(typeof(AvaloniaGenericInputHandler).Name);
    }

    public void Init(IHostInputState hostInputState)
    {
        _hostInput = hostInputState;
    }

    public void BeforeFrame()
    {
        _hostInput.UpdatePerFrame();
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
        // The Generic machine exposes one ASCII key at a time.
        var keys = _hostInput.KeysDown;
        if (keys.Count == 0)
        {
            genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = 0;
            return;
        }

        var key = keys.First();
        foreach (var (physicalKey, character) in GenericComputerAvaloniaKeyboard.AvaloniaToGenericKeyMap)
        {
            if (AvaloniaInputHandlerContext.MapToHostKey(physicalKey) == key)
            {
                genericComputer.Mem[_emulatorInputConfig.KeyDownAddress] = (byte)character;
                return;
            }
        }
    }

    private void CaptureRandomNumber(GenericComputer system)
    {
        var emulatorMem = system.Mem;

        var rnd = (byte)new Random().Next(0, 255);
        emulatorMem[_emulatorInputConfig.RandomValueAddress] = rnd;
    }
}
