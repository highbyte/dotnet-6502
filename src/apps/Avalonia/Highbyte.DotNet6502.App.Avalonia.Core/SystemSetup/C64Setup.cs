using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

public class C64Setup : ISystemConfigurer<AvaloniaInputHandlerContext, NullAudioHandlerContext>
{
    public string SystemName => C64.SystemName;

    private readonly Func<string, Task<string>>? _getCustomConfigJson = null;
    private readonly Func<string, string, Task>? _saveCustomConfigJson = null;

    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig) => Task.FromResult(s_systemVariants);

    private static readonly List<string> s_systemVariants = C64ModelInventory.C64Models.Keys.ToList();

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<C64Setup> _logger;
    private readonly IConfiguration _configuration;

    public C64Setup(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        Func<string, Task<string>>? getCustomConfigJson = null,
        Func<string, string, Task>? saveCustomConfigJson = null)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<C64Setup>();
        _configuration = configuration;
        _getCustomConfigJson = getCustomConfigJson;
        _saveCustomConfigJson = saveCustomConfigJson;
    }

    public async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        if (_getCustomConfigJson == null)
        {
            return await GetNewHostSystemConfigFromAppSettings();
        }

        _logger.LogInformation("Loading C64HostConfig from custom JSON source.");
        // Get config from supplied raw JSON string
        C64HostConfig? hostConfig = null;
        try
        {
            // Get config from supplied raw JSON string
            string jsonString = await _getCustomConfigJson(C64HostConfig.ConfigSectionName);
            if (!string.IsNullOrEmpty(jsonString))
            {
                // Deserialize using a JsonSerializerContext configured for source generation (to be compatible with AOT compilation)
                var deserializedConfig = JsonSerializer.Deserialize(
                    jsonString,
                    HostConfigJsonContext.Default.C64HostConfig);
                if (deserializedConfig != null)
                {
                    _logger.LogInformation("Successfully deserialized C64HostConfig from JSON.");
                    hostConfig = deserializedConfig;

                    // Note: Because ROMDirectory should never be used when running in Browser, make sure it is empty (regardless if config from local storage has it set).
                    hostConfig.SystemConfig.ROMDirectory = "";
               }
                else
                {
                    _logger.LogWarning("Deserialized C64HostConfig is null, using default config.");
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
            hostConfig = new C64HostConfig();
            hostConfig.SystemConfig.ROMDirectory = "";
            hostConfig.SystemConfig.AudioEnabled = false;
        }

        return hostConfig;
    }

    public Task<IHostSystemConfig> GetNewHostSystemConfigFromAppSettings()
    {
        var c64HostConfig = new C64HostConfig();
        _configuration.GetSection($"{C64HostConfig.ConfigSectionName}").Bind(c64HostConfig);
        return Task.FromResult<IHostSystemConfig>(c64HostConfig);
    }

    public async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (_saveCustomConfigJson == null)
        {
            _logger.LogWarning("No method for saving custom config JSON supplied, so not saving C64HostConfig.");
            return;
        }
        var json = JsonSerializer.Serialize(hostSystemConfig, HostConfigJsonContext.Default.C64HostConfig);
        await _saveCustomConfigJson(C64HostConfig.ConfigSectionName, json);
    }

    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var c64SystemConfig = (C64SystemConfig)systemConfig;
        var c64Config = new C64Config
        {
            C64Model = configurationVariant,
            Vic2Model = C64ModelInventory.C64Models[configurationVariant].Vic2Models.First().Name,
            AudioEnabled = c64SystemConfig.AudioEnabled,
            KeyboardJoystickEnabled = c64SystemConfig.KeyboardJoystickEnabled,
            KeyboardJoystick = c64SystemConfig.KeyboardJoystick,
            ROMs = c64SystemConfig.ROMs,
            ROMDirectory = c64SystemConfig.ROMDirectory,
            RenderProviderType = c64SystemConfig.RenderProviderType,
        };

        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return Task.FromResult<ISystem>(c64);
    }

    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        AvaloniaInputHandlerContext inputHandlerContext,
        NullAudioHandlerContext audioHandlerContext
        )
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        var c64 = (C64)system;

        // TODO: Create specific Avalonia input handler
        var inputHandler = new AvaloniaC64InputHandler(c64, inputHandlerContext, _loggerFactory, c64HostConfig.InputConfig);

        var audioHandler = new NullAudioHandler(c64);

        return Task.FromResult(new SystemRunner(c64, inputHandler, audioHandler));
    }
}
