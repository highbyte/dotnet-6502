using System;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64
{
    public static class C64SkiaSystemRunnerBuilder
    {
        public static SystemRunner<C64> BuildSystemRunner(GRContext grContext, SKCanvas sKCanvas)
        {
            var c64 = C64.BuildC64();
            var renderer = new C64SkiaRenderer(sKCanvas);
            renderer.Init(grContext, sKCanvas);
            IInputHandler<C64> inputHandler = null;    // TODO: What library to use for handling input? Should be able to use across platforms incl WASM. Maybe implement a custom emulator-specific input handler abstraction that can be mapped to any library?
            var systemRunner = new SystemRunner<C64>(c64, renderer, inputHandler);
            return systemRunner;
        }
    }
}