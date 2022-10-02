using System;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;

namespace Highbyte.DotNet6502.SadConsoleHost.Commodore64
{
    public static class C64SadConsoleSystemRunnerBuilder
    {
        // public static SystemRunner<C64> BuildSystemRunner(Func<SadConsoleScreenObject> getSadConsoleScreen)
        // {
        //     var c64 = C64.BuildC64();
        //     var renderer = new C64SadConsoleRenderer(getSadConsoleScreen);
        //     var inputHandler = new C64SadConsoleInputHandler();
        //     var systemRunner = new Emulator<C64>(c64)
        //     {
        //         Renderer = renderer,
        //         InputHandler = inputHandler
        //     };
        //     return systemRunner;
        // }

        public static SystemRunner<C64> BuildSystemRunner(Func<SadConsoleScreenObject> getSadConsoleScreen)
        {
            var c64 = C64.BuildC64();
            var renderer = new C64SadConsoleRenderer(getSadConsoleScreen);
            var inputHandler = new C64SadConsoleInputHandler();
            var systemRunner = new SystemRunner<C64>(c64, renderer, inputHandler);
            return systemRunner;
        }
    }
}