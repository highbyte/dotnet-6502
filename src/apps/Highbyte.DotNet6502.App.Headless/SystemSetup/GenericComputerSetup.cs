using System.Reflection;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Headless.SystemSetup;

public class GenericComputerSetup : ISystemConfigurer<NullInputHandlerContext, NullAudioHandlerContext>
{
    public string SystemName => GenericComputer.SystemName;

    private readonly Assembly _examplesAssembly = Assembly.GetExecutingAssembly();
    private string? ExampleFileAssemblyName => _examplesAssembly.GetName().Name;

    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
    {
        var examplePrograms = ((GenericComputerSystemConfig)systemConfig).ExamplePrograms.Keys.ToList();
        return Task.FromResult(examplePrograms);
    }

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    public GenericComputerSetup(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger(nameof(GenericComputerSetup));
        _configuration = configuration;
    }

    public Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        _logger.LogInformation("Loading GenericComputerHostConfig from appsettings.json.");
        var hostConfig = new GenericComputerHostConfig();
        _configuration.GetSection(GenericComputerHostConfig.ConfigSectionName).Bind(hostConfig);

        if (hostConfig.SystemConfig.ExamplePrograms.Count == 0
            || (hostConfig.SystemConfig.ExamplePrograms.Count == 1 && hostConfig.SystemConfig.ExamplePrograms.Keys.First() == "None"))
        {
            hostConfig.SystemConfig.ExamplePrograms = new Dictionary<string, string?>
            {
                { "Scroll", $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Generic.hostinteraction_scroll_text_and_cycle_colors.prg" },
                { "Snake",  $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Generic.snake6502.prg" }
            };
        }

        return Task.FromResult<IHostSystemConfig>(hostConfig);
    }

    public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        _logger.LogWarning("Headless app does not support persisting host system config.");
        return Task.CompletedTask;
    }

    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var genericComputerSystemConfig = (GenericComputerSystemConfig)systemConfig;

        if (!genericComputerSystemConfig.ExamplePrograms.ContainsKey(configurationVariant))
            throw new ArgumentException($"No example program with name '{configurationVariant}' exists in system config.");
        var exampleProgramPath = genericComputerSystemConfig.ExamplePrograms[configurationVariant];

        GenericComputerConfig? genericComputerConfig = null;
        if (!string.IsNullOrEmpty(exampleProgramPath))
        {
            // Check if exampleProgramPath starts with ExampleFileAssemblyName. If not prepend it.
            if (!exampleProgramPath.StartsWith(ExampleFileAssemblyName!))
            {
                exampleProgramPath = $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Generic.{exampleProgramPath}";
            }
            byte[] exampleProgramBytes;
            using (var resourceStream = _examplesAssembly.GetManifestResourceStream(exampleProgramPath))
            {
                if (resourceStream == null)
                    throw new Exception($"Cannot find file in embedded resources. Resource: {exampleProgramPath}");
                exampleProgramBytes = new byte[resourceStream.Length];
                resourceStream.ReadExactly(exampleProgramBytes);
            }
            genericComputerConfig = GenericComputerExampleConfigs.GetExampleConfig(configurationVariant, genericComputerSystemConfig, exampleProgramBytes);
        }

        genericComputerConfig ??= GenericComputerExampleConfigs.GetExampleConfig(configurationVariant, genericComputerSystemConfig);

        return Task.FromResult<ISystem>(GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig, _loggerFactory));
    }

    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        NullInputHandlerContext inputHandlerContext,
        NullAudioHandlerContext audioHandlerContext)
    {
        var genericComputer = (GenericComputer)system;
        var inputHandler = new NullInputHandler(genericComputer);
        var audioHandler = new NullAudioHandler(genericComputer);
        return Task.FromResult(new SystemRunner(genericComputer, inputHandler, audioHandler));
    }
}
