using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Impl.SadConsole.Generic.Input;
using Highbyte.DotNet6502.Impl.SadConsole.Generic.Video;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class GenericComputerSetup : ISystemConfigurer<SadConsoleRenderContext, SadConsoleInputHandlerContext, NAudioAudioHandlerContext>
{
    public string SystemName => GenericComputer.SystemName;

    public Task<List<string>> GetConfigurationVariants(IHostSystemConfig hostSystemConfig)
    {
        var examplePrograms = ((GenericComputerHostConfig)hostSystemConfig).SystemConfig.ExamplePrograms.Keys.OrderByDescending(x => x).ToList();
        return Task.FromResult(examplePrograms);
    }

    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;

    public GenericComputerSetup(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
    }

    public Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var genericComputerHostConfig = new GenericComputerHostConfig { };
        _configuration.GetSection($"{GenericComputerHostConfig.ConfigSectionName}").Bind(genericComputerHostConfig);
        return Task.FromResult<IHostSystemConfig>(genericComputerHostConfig);
    }

    public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        // TODO: Should user settings be persisted? If so method GetNewHostSystemConfig() also needs to be updated to read from there instead of appsettings.json.
        return Task.CompletedTask;
    }

    public Task<ISystem> BuildSystem(string configurationVariant, IHostSystemConfig hostSystemConfig)
    {
        var genericComputerHostConfig = (GenericComputerHostConfig)hostSystemConfig;
        var exampleProgramPath = genericComputerHostConfig.SystemConfig.ExamplePrograms[configurationVariant];
        var genericComputerConfig = GenericComputerExampleConfigs.GetExampleConfig(configurationVariant, exampleProgramPath);

        return Task.FromResult<ISystem>(
            GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig, _loggerFactory)
        );
    }

    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        SadConsoleRenderContext renderContext,
        SadConsoleInputHandlerContext inputHandlerContext,
        NAudioAudioHandlerContext audioHandlerContext)
    {
        var genericComputer = (GenericComputer)system;
        var genericComputerHostConfig = (GenericComputerHostConfig)hostSystemConfig;
        var genericComputerConfig = genericComputerHostConfig.SystemConfig;

        var renderer = new GenericSadConsoleRenderer(genericComputer, renderContext, genericComputer.GenericComputerConfig.Memory.Screen);
        var inputHandler = new GenericSadConsoleInputHandler(genericComputer, inputHandlerContext, genericComputer.GenericComputerConfig.Memory.Input, _loggerFactory);
        var audioHandler = new NullAudioHandler(genericComputer);

        return Task.FromResult(new SystemRunner(genericComputer, renderer, inputHandler, audioHandler));
    }
}
