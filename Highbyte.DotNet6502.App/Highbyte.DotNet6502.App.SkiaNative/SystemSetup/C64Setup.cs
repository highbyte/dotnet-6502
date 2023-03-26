using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.SkiaNative.SystemSetup;

public class C64Setup
{
    public C64Setup()
    {
    }

    public async Task<ISystemConfig> GetNewConfig(string configurationVariant)
    {
        var c64Config = new C64Config
        {
            C64Model = "C64NTSC",   // C64NTSC, C64PAL
            Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
                                    // C64Model = "C64PAL",   // C64NTSC, C64PAL
                                    // Vic2Model = "PAL",     // NTSC, NTSC_old, PAL
                                    //ROMDirectory = "%USERPROFILE%/Documents/C64/VICE/C64",
            ROMDirectory = "%HOME%/Downloads/C64",
            ROMs = new List<ROM>
            {
                new ROM
                {
                    Name = C64Config.BASIC_ROM_NAME,
                    File = "basic.901226-01.bin",
                    Data = null,
                    Checksum = "79015323128650c742a3694c9429aa91f355905e",
                },
                new ROM
                {
                    Name = C64Config.CHARGEN_ROM_NAME,
                    File = "characters.901225-01.bin",
                    Data = null,
                    Checksum = "adc7c31e18c7c7413d54802ef2f4193da14711aa",
                },
                new ROM
                {
                    Name = C64Config.KERNAL_ROM_NAME,
                    File = "kernal.901227-03.bin",
                    Data = null,
                    Checksum = "1d503e56df85a62fee696e7618dc5b4e781df1bb",
                }
            }
        };

        c64Config.Validate();
        return c64Config;
    }

    public async Task PersistConfig(ISystemConfig systemConfig)
    {
        var c64Config = (C64Config)systemConfig;
        // TODO: Persist settings to file
    }

    public ISystem BuildSystem(ISystemConfig systemConfig)
    {
        var c64Config = (C64Config)systemConfig;
        var c64 = C64.BuildC64(c64Config);
        return c64;
    }

    public SystemRunner BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        SkiaRenderContext renderContext,
        SilkNetInputHandlerContext inputHandlerContext
        )
    {
        var renderer = new C64SkiaRenderer();
        var inputHandler = new C64SilkNetInputHandler();

        var c64 = (C64)system;
        renderer.Init(c64, renderContext);
        inputHandler.Init(c64, inputHandlerContext);

        var systemRunnerBuilder = new SystemRunnerBuilder<C64, SkiaRenderContext, SilkNetInputHandlerContext>(c64);
        var systemRunner = systemRunnerBuilder
            .WithRenderer(renderer)
            .WithInputHandler(inputHandler)
            .Build();
        return systemRunner;
    }
}
