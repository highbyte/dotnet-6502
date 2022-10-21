using System;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64;
using Highbyte.DotNet6502.Impl.SadConsole.Generic;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.Impl.SadConsole
{
    public class EmulatorHost
    {
        private readonly SadConsoleConfig _sadConsoleConfig;
        private readonly GenericComputerConfig _genericComputerConfig;
        private readonly C64Config _c64Config;
        private static SadConsoleMain SadConsoleMain;

        public EmulatorHost(
            SadConsoleConfig sadConsoleConfig,
            GenericComputerConfig genericComputerConfig,
            C64Config c64Config
            )
        {
            _sadConsoleConfig = sadConsoleConfig;
            _genericComputerConfig = genericComputerConfig;
            _c64Config = c64Config;
        }

        public void Start()
        {

            SystemRunner systemRunner;

            var sadConsoleRenderContext = new SadConsoleRenderContext(GetSadConsoleScreen);
            var sadConsoleInputHandlerContext= new SadConsoleInputHandlerContext();

            switch (_sadConsoleConfig.Emulator)
            {
                case "GenericComputer":
                    systemRunner = GetGenericSystemRunner(sadConsoleRenderContext, sadConsoleInputHandlerContext);
                    break;

                case "C64":
                    systemRunner = GetC64SystemRunner(sadConsoleRenderContext, sadConsoleInputHandlerContext);
                    break;

                default:
                    throw new Exception($"Unknown emulator name: {_sadConsoleConfig.Emulator}");
            }

            if (systemRunner.System is not ITextMode)
                throw new Exception("SadConsole host only supports running emulator systems that supports text mode.");

            // Create the main SadConsole class that is responsible for configuring and starting up SadConsole and running the emulator code every frame with our preferred configuration.
            SadConsoleMain = new SadConsoleMain(
                _sadConsoleConfig,
                systemRunner);

            // Start SadConsole. Will exit from this method after SadConsole window is closed.
            SadConsoleMain.Run();
        }

        private SadConsoleScreenObject GetSadConsoleScreen()
        {
            return SadConsoleMain.SadConsoleScreen;
        }

        SystemRunner GetC64SystemRunner(SadConsoleRenderContext sadConsoleRenderContext, SadConsoleInputHandlerContext sadConsoleInputHandlerContext)
        {
            var c64 = C64.BuildC64(_c64Config);

            var renderer = new C64SadConsoleRenderer();
            renderer.Init(c64, sadConsoleRenderContext);

            var inputHandler = new C64SadConsoleInputHandler();
            inputHandler.Init(c64, sadConsoleInputHandlerContext);

            var systemRunnerBuilder = new SystemRunnerBuilder<C64, SadConsoleRenderContext, SadConsoleInputHandlerContext>(c64);
            var systemRunner = systemRunnerBuilder
                .WithRenderer(renderer)
                .WithInputHandler(inputHandler)
                .Build();
            return systemRunner;
        }


        SystemRunner GetGenericSystemRunner(SadConsoleRenderContext sadConsoleRenderContext, SadConsoleInputHandlerContext sadConsoleInputHandlerContext)
        {
            var genericComputer = GenericComputerBuilder.SetupGenericComputerFromConfig(_genericComputerConfig);

            var renderer = new GenericSadConsoleRenderer(_genericComputerConfig.Memory.Screen);
            renderer.Init(genericComputer, sadConsoleRenderContext);

            var inputHandler = new GenericSadConsoleInputHandler(_genericComputerConfig.Memory.Input);
            inputHandler.Init(genericComputer, sadConsoleInputHandlerContext);

            var systemRunnerBuilder = new SystemRunnerBuilder<GenericComputer, SadConsoleRenderContext, SadConsoleInputHandlerContext>(genericComputer);
            var systemRunner = systemRunnerBuilder
                .WithRenderer(renderer)
                .WithInputHandler(inputHandler)
                .Build();
            return systemRunner;
        }

    }
}
