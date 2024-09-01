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
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;

public class C64Setup : ISystemConfigurer<SilkNetRenderContextContainer, SilkNetInputHandlerContext, NAudioAudioHandlerContext>
{
    public string SystemName => C64.SystemName;
    public List<string> ConfigurationVariants => s_systemVariants;

    private static readonly List<string> s_systemVariants = C64ModelInventory.C64Models.Keys.ToList();

    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;

    public C64Setup(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
    }

    public IHostSystemConfig GetNewHostSystemConfig()
    {
        // TODO: Read System host config from appsettings.json
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


    public Task<ISystemConfig> GetNewConfig(string configurationVariant)
    {
        if (!s_systemVariants.Contains(configurationVariant))
            throw new ArgumentException($"Unknown configuration variant '{configurationVariant}'.");

        var c64Config = new C64Config() { ROMs = new() };
        _configuration.GetSection($"{C64Config.ConfigSectionName}.{configurationVariant}").Bind(c64Config);
        c64Config.SetROMDefaultChecksums();
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
