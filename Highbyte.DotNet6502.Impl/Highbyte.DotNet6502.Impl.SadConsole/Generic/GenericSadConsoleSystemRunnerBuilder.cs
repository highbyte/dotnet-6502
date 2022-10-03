using System;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.SadConsole.Generic
{
    public static class GenericSadConsoleSystemRunnerBuilder
    {
        // public static SystemRunner<Computer> BuildSystemRunner(
        //     Computer genericComputer,
        //     Func<SadConsoleScreenObject> getSadConsoleScreen,
        //     EmulatorScreenConfig emulatorScreenConfig,
        //     EmulatorInputConfig emulatorInputConfig)
        // {
        //     var renderer = new GenericSadConsoleRenderer(getSadConsoleScreen, emulatorScreenConfig);
        //     renderer.InitEmulatorScreenMemory(genericComputer);

        //     var inputHandler = new GenericSadConsoleInputHandler(emulatorInputConfig);

        //     var systemRunner = new SystemRunner<Computer>(genericComputer)
        //     {
        //         Renderer = renderer,
        //         InputHandler = inputHandler
        //     };
        //     return systemRunner;
        // }


        public static SystemRunner<GenericComputer> BuildSystemRunner(
            GenericComputer genericComputer,
            Func<SadConsoleScreenObject> getSadConsoleScreen,
            EmulatorScreenConfig emulatorScreenConfig,
            EmulatorInputConfig emulatorInputConfig)
        {
            var renderer = new GenericSadConsoleRenderer(getSadConsoleScreen, emulatorScreenConfig);
            renderer.InitEmulatorScreenMemory(genericComputer);

            var inputHandler = new GenericSadConsoleInputHandler(emulatorInputConfig);

            var systemRunner = new SystemRunner<GenericComputer>(genericComputer, renderer, inputHandler);
            return systemRunner;
        }
    }
}