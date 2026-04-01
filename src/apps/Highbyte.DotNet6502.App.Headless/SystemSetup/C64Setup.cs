using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Headless.SystemSetup;

public class C64Setup : ISystemConfigurer<NullInputHandlerContext, NullAudioHandlerContext>
{
    public string SystemName => C64.SystemName;

    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
        => Task.FromResult(C64ModelInventory.C64Models.Keys.ToList());

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    public C64Setup(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger(nameof(C64Setup));
        _configuration = configuration;
    }

    public Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var c64HostConfig = new C64HostConfig();
        _logger.LogInformation("Loading C64HostConfig from IConfiguration.");
        _configuration.GetSection(C64HostConfig.ConfigSectionName).Bind(c64HostConfig);
        _logger.LogInformation("Successfully loaded C64HostConfig from IConfiguration.");
        return Task.FromResult<IHostSystemConfig>(c64HostConfig);
    }

    public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        _logger.LogWarning("Headless app does not support persisting host system config.");
        return Task.CompletedTask;
    }

    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var c64SystemConfig = (C64SystemConfig)systemConfig;
        var c64Config = new C64Config
        {
            C64Model = configurationVariant,
            Vic2Model = C64ModelInventory.C64Models[configurationVariant].Vic2Models.First().Name,
            AudioEnabled = false, // No audio in headless mode
            KeyboardJoystickEnabled = false,
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
        NullInputHandlerContext inputHandlerContext,
        NullAudioHandlerContext audioHandlerContext)
    {
        var c64 = (C64)system;
        var inputHandler = new NullInputHandler(c64);
        var audioHandler = new NullAudioHandler(c64);
        return Task.FromResult(new SystemRunner(c64, inputHandler, audioHandler));
    }
}
