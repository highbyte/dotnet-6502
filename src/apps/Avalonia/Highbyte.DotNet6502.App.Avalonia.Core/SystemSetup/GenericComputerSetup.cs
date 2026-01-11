using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Impl.Avalonia.Generic.Input;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

public class GenericComputerSetup : ISystemConfigurer<AvaloniaInputHandlerContext, NAudioAudioHandlerContext>
{
    public string SystemName => GenericComputer.SystemName;

    private readonly Func<string, string, string?, Task>? _saveCustomConfigJson = null;

    private readonly Assembly _examplesAssembly = Assembly.GetExecutingAssembly();
    private string? ExampleFileAssemblyName => _examplesAssembly.GetName().Name;

    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
    {
        var examplePrograms = ((GenericComputerSystemConfig)systemConfig).ExamplePrograms.Keys
            //.OrderByDescending(x => x)
            .ToList();
        return Task.FromResult(examplePrograms);
    }

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GenericComputerSetup> _logger;
    private readonly IConfiguration _configuration;

    public GenericComputerSetup(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        Func<string, string, string?, Task>? saveCustomConfigJson = null)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<GenericComputerSetup>();
        _configuration = configuration;
        _saveCustomConfigJson = saveCustomConfigJson;
    }

    public async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        _logger.LogInformation("Loading GenericComputerHostConfig from appsettings.json.");

        var hostConfig = new GenericComputerHostConfig();
        _configuration.GetSection($"{GenericComputerHostConfig.ConfigSectionName}").Bind(hostConfig);

        if (hostConfig.SystemConfig.ExamplePrograms.Count == 0 || (hostConfig.SystemConfig.ExamplePrograms.Count == 1 && hostConfig.SystemConfig.ExamplePrograms.Keys.First() == "None"))
        {
            hostConfig.SystemConfig.ExamplePrograms = new Dictionary<string, string?>
            {
                { "Scroll", $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Generic.hostinteraction_scroll_text_and_cycle_colors.prg" },
                { "Snake",  $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Generic.snake6502.prg" }
            };
        }

        // foreach (var kvp in hostConfig.SystemConfig.ExamplePrograms)
        // {
        //     _logger.LogInformation($"Example program: {kvp.Key} => {kvp.Value}");
        // }
        return hostConfig;
    }

    public async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (_saveCustomConfigJson == null)
            return;

        var genericComputerHostConfig = (GenericComputerHostConfig)hostSystemConfig;
        await _saveCustomConfigJson(GenericComputerHostConfig.ConfigSectionName, JsonSerializer.Serialize(genericComputerHostConfig), null);
    }

    public async Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var genericComputerSystemConfig = (GenericComputerSystemConfig)systemConfig ?? throw new ArgumentException($"systemConfig is not of type {nameof(GenericComputerSystemConfig)}");

        if (!genericComputerSystemConfig.ExamplePrograms.ContainsKey(configurationVariant))
            throw new ArgumentException($"No example program with name '{configurationVariant}' exists in system config.");
        var exampleProgramPath = genericComputerSystemConfig.ExamplePrograms[configurationVariant];

        GenericComputerConfig? genericComputerConfig = null;
        if (!string.IsNullOrEmpty(exampleProgramPath))
        {
            try
            {
                // Check if exampleProgramPath starts with ExampleFileAssemblyName. If not prepend it.
                if (!exampleProgramPath.StartsWith(ExampleFileAssemblyName!))
                {
                    exampleProgramPath = $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Generic.{exampleProgramPath}";
                }
                var file = exampleProgramPath;
                byte[] exampleProgramBytes;
                // Load the .prg file from embedded resource
                using (var resourceStream = _examplesAssembly.GetManifestResourceStream(file))
                {
                    if (resourceStream == null)
                        throw new Exception($"Cannot find file in embedded resources. Resource: {file}");
                    // Read contents of stream as byte array
                    exampleProgramBytes = new byte[resourceStream.Length];
                    resourceStream.ReadExactly(exampleProgramBytes);
                }
                genericComputerConfig = GenericComputerExampleConfigs.GetExampleConfig(configurationVariant, genericComputerSystemConfig, exampleProgramBytes);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        if (genericComputerConfig == null)
        {
            genericComputerConfig = GenericComputerExampleConfigs.GetExampleConfig(configurationVariant, genericComputerSystemConfig);
        }

        return GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig, _loggerFactory);
    }

    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        AvaloniaInputHandlerContext inputHandlerContext,
        NAudioAudioHandlerContext audioHandlerContext
        )
    {
        var genericComputer = (GenericComputer)system;
        var genericComputerHostConfig = (GenericComputerHostConfig)hostSystemConfig;
        //var genericComputerConfig = genericComputerHostConfig.SystemConfig;

        // Create specific Avalonia input handler for Generic computer
        var inputHandler = new AvaloniaGenericInputHandler(
            genericComputer,
            inputHandlerContext,
            genericComputer.GenericComputerConfig.Memory.Input,
            _loggerFactory);

        // Generic computer doesn't use audio
        var audioHandler = new NullAudioHandler(genericComputer);

        return Task.FromResult(new SystemRunner(genericComputer, inputHandler, audioHandler));
    }
}
