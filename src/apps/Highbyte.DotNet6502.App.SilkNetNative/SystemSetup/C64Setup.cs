using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.Commodore64.Audio;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v1;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v2;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v3;
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
    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig) => Task.FromResult(s_systemVariants);

    private static readonly List<string> s_systemVariants = C64ModelInventory.C64Models.Keys.ToList();

    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;

    public C64Setup(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
    }

    public Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var c64HostConfig = new C64HostConfig();
        _configuration.GetSection($"{C64HostConfig.ConfigSectionName}").Bind(c64HostConfig);
        return Task.FromResult<IHostSystemConfig>(c64HostConfig);
    }

    public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        // TODO: Should user settings be persisted? If so method GetNewHostSystemConfig() also needs to be updated to read from there instead of appsettings.json.
        return Task.CompletedTask;
    }
    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var c64SystemConfig = (C64SystemConfig)systemConfig;
        var c64Config = new C64Config
        {
            C64Model = configurationVariant,
            Vic2Model = C64ModelInventory.C64Models[configurationVariant].Vic2Models.First().Name, // NTSC, NTSC_old, PAL
            AudioEnabled = c64SystemConfig.AudioEnabled,
            KeyboardJoystickEnabled = c64SystemConfig.KeyboardJoystickEnabled,
            KeyboardJoystick = c64SystemConfig.KeyboardJoystick,
            ROMs = c64SystemConfig.ROMs,
            ROMDirectory = c64SystemConfig.ROMDirectory,
        };

        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return Task.FromResult<ISystem>(c64);
    }

    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
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
            case C64HostRenderer.SkiaSharp3:
                renderer = new C64SkiaRenderer3(c64, renderContextContainer.SkiaRenderContext);
                break;
            case C64HostRenderer.SilkNetOpenGl:
                renderer = new C64SilkNetOpenGlRenderer(c64, renderContextContainer.SilkNetOpenGlRenderContext, c64HostConfig.SilkNetOpenGlRendererConfig);
                break;
            default:
                throw new NotImplementedException($"Renderer {c64HostConfig.Renderer} not implemented.");
        }

        var inputHandler = new C64SilkNetInputHandler(c64, inputHandlerContext, _loggerFactory, c64HostConfig.InputConfig);
        var audioHandler = new C64NAudioAudioHandler(c64, audioHandlerContext, _loggerFactory);

        return Task.FromResult(new SystemRunner(c64, renderer, inputHandler, audioHandler));
    }
}
