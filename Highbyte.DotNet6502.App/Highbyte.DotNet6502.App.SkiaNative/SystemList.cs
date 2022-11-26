using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64;
using Highbyte.DotNet6502.Impl.SilkNet.Generic;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Commodore64;
using Highbyte.DotNet6502.Impl.Skia.Generic;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.App.SkiaNative;

public class SystemList
{
    /// <summary>
    /// Systems that are available for running with a native Silk.Net & Skia host.
    /// </summary>
    public static HashSet<string> SystemNames = new();

    static SystemList()
    {
        SystemNames.Add(C64.SystemName);
        SystemNames.Add(GenericComputer.SystemName);
    }

    private readonly C64Config _c64Config;
    private readonly GenericComputerConfig _genericComputerConfig;

    public SystemList(C64Config c64Config, GenericComputerConfig genericComputerConfig)
    {
        _c64Config = c64Config;
        _genericComputerConfig = genericComputerConfig;
    }

    public ISystem BuildSystem(string systemName)
    {
        ISystem system;
        switch (systemName)
        {
            case C64.SystemName:
                system = C64.BuildC64(_c64Config);
                break;

            case GenericComputer.SystemName:
                system = GenericComputerBuilder.SetupGenericComputerFromConfig(_genericComputerConfig);
                break;

            default:
                throw new ArgumentException("Unknown system", nameof(systemName));
        }
        return system;
    }

    // Functions for building SystemRunner based on Skia rendering.
    // Will be used as from SilkNetWindow in OnLoad (when OpenGL context has been created.)
    public SystemRunner GetSystemRunner(ISystem system, SkiaRenderContext skiaRenderContext, SilkNetInputHandlerContext silkNetInputHandlerContext)
    {
        if (system is C64 c64)
        {
            var renderer = new C64SkiaRenderer();
            renderer.Init(system, skiaRenderContext);

            var inputHandler = new C64SilkNetInputHandler();
            inputHandler.Init(system, silkNetInputHandlerContext);

            var systemRunnerBuilder = new SystemRunnerBuilder<C64, SkiaRenderContext, SilkNetInputHandlerContext>(c64);
            var systemRunner = systemRunnerBuilder
                .WithRenderer(renderer)
                .WithInputHandler(inputHandler)
                .Build();
            return systemRunner;
        }

        if (system is GenericComputer genericComputer)
        {
            var renderer = new GenericComputerSkiaRenderer(_genericComputerConfig.Memory.Screen);
            renderer.Init(system, skiaRenderContext);

            var inputHandler = new GenericComputerSilkNetInputHandler(_genericComputerConfig.Memory.Input);
            inputHandler.Init(system, silkNetInputHandlerContext);

            var systemRunnerBuilder = new SystemRunnerBuilder<GenericComputer, SkiaRenderContext, SilkNetInputHandlerContext>(genericComputer);
            var systemRunner = systemRunnerBuilder
                .WithRenderer(renderer)
                .WithInputHandler(inputHandler)
                .Build();
            return systemRunner;
        }

        throw new NotImplementedException($"System not handled: {system.Name}");
    }
}
