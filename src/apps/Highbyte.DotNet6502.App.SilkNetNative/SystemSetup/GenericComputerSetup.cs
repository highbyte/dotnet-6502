using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Generic.Input;
using Highbyte.DotNet6502.Impl.Skia.Generic.Video;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;

public class GenericComputerSetup : ISystemConfigurer<SilkNetRenderContextContainer, SilkNetInputHandlerContext, NAudioAudioHandlerContext>
{
    public string SystemName => GenericComputer.SystemName;

    public List<string> ConfigurationVariants => s_systemVariants;

    private static readonly List<string> s_systemVariants =
    [
        "Scroll",
        "Snake",
        "HelloWorld",
    ];

    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;

    public GenericComputerSetup(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
    }

    public Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        // TODO: Read System host config from appsettings.json
        var genericComputerHostConfig = new GenericComputerHostConfig { };
        return Task.FromResult<IHostSystemConfig>(genericComputerHostConfig);
    }

    public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        // TODO: Persist settings to file
        return Task.CompletedTask;
    }

    public Task<ISystemConfig> GetNewConfig(string configurationVariant)
    {
        if (!s_systemVariants.Contains(configurationVariant))
            throw new ArgumentException($"Unknown configuration variant '{configurationVariant}'.");

        var genericComputerConfig = new GenericComputerConfig() { };
        _configuration.GetSection($"{GenericComputerConfig.ConfigSectionName}.{configurationVariant}").Bind(genericComputerConfig);
        return Task.FromResult<ISystemConfig>(genericComputerConfig);
    }

    public Task PersistConfig(ISystemConfig systemConfig)
    {
        var genericComputerConfig = (GenericComputerConfig)systemConfig;
        // TODO: Save config settings to file
        return Task.CompletedTask;
    }

    public ISystem BuildSystem(ISystemConfig systemConfig)
    {
        var genericComputerConfig = (GenericComputerConfig)systemConfig;
        return GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig, _loggerFactory);
    }

    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        IHostSystemConfig hostSystemConfig,
        SilkNetRenderContextContainer renderContext,
        SilkNetInputHandlerContext inputHandlerContext,
        NAudioAudioHandlerContext audioHandlerContext)
    {
        var genericComputer = (GenericComputer)system;
        var genericComputerConfig = (GenericComputerConfig)systemConfig;

        var renderer = new GenericComputerSkiaRenderer(genericComputer, renderContext.SkiaRenderContext, genericComputerConfig.Memory.Screen);
        var inputHandler = new GenericComputerSilkNetInputHandler(genericComputer, inputHandlerContext, genericComputerConfig.Memory.Input);
        var audioHandler = new NullAudioHandler(genericComputer);

        return Task.FromResult(new SystemRunner(genericComputer, renderer, inputHandler, audioHandler));
    }
}
