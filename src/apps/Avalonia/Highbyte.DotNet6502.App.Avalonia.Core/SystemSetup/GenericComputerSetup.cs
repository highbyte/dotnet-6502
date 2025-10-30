using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

public class GenericComputerSetup : ISystemConfigurer<AvaloniaInputHandlerContext, NullAudioHandlerContext>
{
    public string SystemName => GenericComputer.SystemName;

    private readonly Func<string, Task<string>>? _getCustomConfigJson = null;
    private readonly Func<string, string, Task>? _saveCustomConfigJson = null;

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
    private readonly EmulatorConfig _emulatorConfig;
    private readonly HttpClient? _appUrlHttpClient;

    public GenericComputerSetup(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        Func<string, Task<string>>? getCustomConfigJson = null,
        Func<string, string, Task>? saveCustomConfigJson = null)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<GenericComputerSetup>();
        _configuration = configuration;
        _emulatorConfig = emulatorConfig;
        _appUrlHttpClient = emulatorConfig.GetAppUrlHttpClient();
        _getCustomConfigJson = getCustomConfigJson;
        _saveCustomConfigJson = saveCustomConfigJson;
    }

    public async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        if (_getCustomConfigJson == null)
        {
            return await GetNewHostSystemConfigFromAppSettings();
        }

        _logger.LogInformation("Loading GenericComputerHostConfig from custom JSON source.");
        // Get config from supplied raw JSON string
        GenericComputerHostConfig? hostConfig = null;
        try
        {
            // Get config from supplied raw JSON string
            string jsonString = await _getCustomConfigJson(GenericComputerHostConfig.ConfigSectionName);

            if (!string.IsNullOrEmpty(jsonString))
            {
                // Deserialize using a JsonSerializerContext configured for source generation (to be compatible with AOT compilation)
                var deserializedConfig = JsonSerializer.Deserialize(
                    jsonString,
                    HostConfigJsonContext.Default.GenericComputerHostConfig);

                if (deserializedConfig != null)
                {
                    _logger.LogInformation("Successfully deserialized GenericComputerHostConfig from JSON.");
                    hostConfig = deserializedConfig;
                }
                else
                {
                    _logger.LogWarning("Deserialized GenericComputerHostConfig is null, using default config.");
                }
            }
        }
        catch (Exception ex)
        {
            // Log error and continue with default config
            _logger.LogWarning(ex, "Failed to load config from JSON, using default config");
        }

        if (hostConfig == null)
        {
            _logger.LogWarning("No JSON config available, using default config.");
            hostConfig = new GenericComputerHostConfig();
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

    private Task<IHostSystemConfig> GetNewHostSystemConfigFromAppSettings()
    {
        _logger.LogInformation("Loading GenericComputerHostConfig from appsettings.json.");

        var genericHostConfig = new GenericComputerHostConfig();
        _configuration.GetSection($"{GenericComputerHostConfig.ConfigSectionName}").Bind(genericHostConfig);
        return Task.FromResult<IHostSystemConfig>(genericHostConfig);
    }

    public async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (_saveCustomConfigJson == null)
            return;

        var genericComputerHostConfig = (GenericComputerHostConfig)hostSystemConfig;
        await _saveCustomConfigJson(GenericComputerHostConfig.ConfigSectionName, JsonSerializer.Serialize(genericComputerHostConfig));
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
        NullAudioHandlerContext audioHandlerContext
        )
    {
        var genericComputer = (GenericComputer)system;
        var genericComputerHostConfig = (GenericComputerHostConfig)hostSystemConfig;
        var genericComputerConfig = genericComputerHostConfig.SystemConfig;

        // TODO: Create specific Avalonia input handler for Generic computer
        var inputHandler = new AvaloniaGenericInputHandler(genericComputer, inputHandlerContext, _loggerFactory);

        // Generic computer doesn't use audio
        var audioHandler = new NullAudioHandler(genericComputer);

        return Task.FromResult(new SystemRunner(genericComputer, inputHandler, audioHandler));
    }
}
