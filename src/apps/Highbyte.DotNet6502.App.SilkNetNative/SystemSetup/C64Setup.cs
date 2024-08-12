using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.Commodore64.Audio;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v1;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v2;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;

public class C64Setup : ISystemConfigurer<SilkNetRenderContextContainer, SilkNetInputHandlerContext, NAudioAudioHandlerContext>
{
    public string SystemName => C64.SystemName;

    private static readonly List<string> s_systemVariants =
    [
        "C64NTSC",
        "C64PAL",
    ];

    private readonly ILoggerFactory _loggerFactory;

    public C64Setup(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IHostSystemConfig GetNewHostSystemConfig()
    {
        var c64HostConfig = new C64HostConfig
        {
            Renderer = C64HostRenderer.SkiaSharp2b,
            SilkNetOpenGlRendererConfig = new C64SilkNetOpenGlRendererConfig()
            {
                UseFineScrollPerRasterLine = false, // Setting to true may work, depending on how code is written. Full screen scroll may not work (actual screen memory is not rendered in sync with raster line).
            }
        };
        return c64HostConfig;
    }

    public List<string> GetConfigurationVariants()
    {
        return s_systemVariants;
    }

    public Task<ISystemConfig> GetNewConfig(string configurationVariant)
    {
        string c64Model;
        string vic2Model;
        switch (configurationVariant.ToUpper())
        {
            case "DEFAULT":
            case "C64NTSC":
                c64Model = "C64NTSC";
                vic2Model = "NTSC"; // NTSC, NTSC_old
                break;
            case "C64PAL":
                c64Model = "C64PAL";
                vic2Model = "PAL";
                break;
            default:
                throw new ArgumentException($"Unknown configuration variant '{configurationVariant}'.");
        }

        var c64Config = new C64Config
        {
            C64Model = c64Model,
            Vic2Model = vic2Model,

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
            },

            AudioSupported = true,
            AudioEnabled = true,

            InstrumentationEnabled = false, // Start with instrumentation off by default
        };

        //c64Config.Validate();
        return Task.FromResult<ISystemConfig>(c64Config);
    }

    public Task PersistConfig(ISystemConfig systemConfig)
    {
        var c64Config = (C64Config)systemConfig;
        // TODO: Persist settings to file

        return Task.CompletedTask;
    }

    public ISystem BuildSystem(ISystemConfig systemConfig)
    {
        var c64Config = (C64Config)systemConfig;
        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return c64;
    }

    public SystemRunner BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        IHostSystemConfig hostSystemConfig,
        SilkNetRenderContextContainer renderContextContainer,
        SilkNetInputHandlerContext inputHandlerContext,
        NAudioAudioHandlerContext audioHandlerContext
        )
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        var c64 = (C64)system;

        IRenderer renderer;
        switch (c64HostConfig.Renderer)
        {
            case C64HostRenderer.SkiaSharp:
                renderer = new C64SkiaRenderer(c64, renderContextContainer.SkiaRenderContext);
                break;
            case C64HostRenderer.SkiaSharp2:
                renderer = new C64SkiaRenderer2(c64, renderContextContainer.SkiaRenderContext);
                break;
            case C64HostRenderer.SkiaSharp2b:
                renderer = new C64SkiaRenderer2b(c64, renderContextContainer.SkiaRenderContext);
                break;
            case C64HostRenderer.SilkNetOpenGl:
                renderer = new C64SilkNetOpenGlRenderer(c64, renderContextContainer.SilkNetOpenGlRenderContext, c64HostConfig.SilkNetOpenGlRendererConfig);
                break;
            default:
                throw new NotImplementedException($"Renderer {c64HostConfig.Renderer} not implemented.");
        }

        var inputHandler = new C64SilkNetInputHandler(c64, inputHandlerContext, _loggerFactory, c64HostConfig.InputConfig);
        var audioHandler = new C64NAudioAudioHandler(c64, audioHandlerContext, _loggerFactory);

        return new SystemRunner(c64, renderer, inputHandler, audioHandler);
    }
}
