using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Impl.SadConsole.Generic.Video;
using Highbyte.DotNet6502.Impl.SadConsole.Generic.Input;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Video;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Input;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SadConsole;

public class EmulatorHost
{
    private readonly SadConsoleConfig _sadConsoleConfig;
    private readonly GenericComputerConfig _genericComputerConfig;
    private readonly C64Config _c64Config;
    private static SadConsoleMain s_sadConsoleMain = default!;
    private readonly ILoggerFactory _loggerFactory;

    public EmulatorHost(
        SadConsoleConfig sadConsoleConfig,
        GenericComputerConfig genericComputerConfig,
        C64Config c64Config,
        ILoggerFactory loggerFactory
        )
    {
        _sadConsoleConfig = sadConsoleConfig;
        _genericComputerConfig = genericComputerConfig;
        _c64Config = c64Config;
        _loggerFactory = loggerFactory;
    }

    public void Start()
    {

        SystemRunner systemRunner;

        var sadConsoleRenderContext = new SadConsoleRenderContext(GetSadConsoleScreen);
        sadConsoleRenderContext.Init();

        var sadConsoleInputHandlerContext = new SadConsoleInputHandlerContext(_loggerFactory);
        sadConsoleInputHandlerContext.Init();

        switch (_sadConsoleConfig.Emulator)
        {
            case "GenericComputer":
                systemRunner = GetGenericSystemRunner(sadConsoleRenderContext, sadConsoleInputHandlerContext);
                break;

            case "C64":
                systemRunner = GetC64SystemRunner(sadConsoleRenderContext, sadConsoleInputHandlerContext);
                break;

            default:
                throw new DotNet6502Exception($"Unknown emulator name: {_sadConsoleConfig.Emulator}");
        }

        systemRunner.Init();

        if (systemRunner.System.Screen is not ITextMode)
            throw new DotNet6502Exception("SadConsole host only supports running emulator systems that supports text mode.");

        // Create the main SadConsole class that is responsible for configuring and starting up SadConsole and running the emulator code every frame with our preferred configuration.
        s_sadConsoleMain = new SadConsoleMain(
            _sadConsoleConfig,
            systemRunner);

        // Start SadConsole. Will exit from this method after SadConsole window is closed.
        s_sadConsoleMain.Run();
    }

    private SadConsoleScreenObject GetSadConsoleScreen()
    {
        return s_sadConsoleMain.SadConsoleScreen;
    }

    private SystemRunner GetC64SystemRunner(SadConsoleRenderContext sadConsoleRenderContext, SadConsoleInputHandlerContext sadConsoleInputHandlerContext)
    {
        var c64 = C64.BuildC64(_c64Config, _loggerFactory);
        var renderer = new C64SadConsoleRenderer(c64, sadConsoleRenderContext);
        var inputHandler = new C64SadConsoleInputHandler(c64, sadConsoleInputHandlerContext, _loggerFactory);
        return new SystemRunner(c64, renderer, inputHandler);
    }

    private SystemRunner GetGenericSystemRunner(SadConsoleRenderContext renderContext, SadConsoleInputHandlerContext inputHandlerContext)
    {
        var genericComputer = GenericComputerBuilder.SetupGenericComputerFromConfig(_genericComputerConfig, _loggerFactory);
        var renderer = new GenericSadConsoleRenderer(genericComputer, renderContext, _genericComputerConfig.Memory.Screen);
        var inputHandler = new GenericSadConsoleInputHandler(genericComputer, inputHandlerContext, _genericComputerConfig.Memory.Input, _loggerFactory);
        return new SystemRunner(genericComputer, renderer, inputHandler);
    }
}
