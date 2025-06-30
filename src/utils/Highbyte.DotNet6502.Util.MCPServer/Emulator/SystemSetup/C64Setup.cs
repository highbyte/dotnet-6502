using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Util.MCPServer.Emulator.SystemSetup;
internal class C64Setup : ISystemConfigurer<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext>
{
    public string SystemName => C64.SystemName;

    public Task<List<string>> GetConfigurationVariants(IHostSystemConfig hostSystemConfig) => Task.FromResult(s_systemVariants);
    public List<string> ConfigurationVariants => s_systemVariants;

    private static readonly List<string> s_systemVariants = C64ModelInventory.C64Models.Keys.ToList();

    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;


    internal C64Setup(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
    }

    public Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var c64HostConfig = new C64HostConfig();
        _configuration.GetSection($"{C64HostConfig.ConfigSectionName}").Bind(c64HostConfig);

        // TODO: Why is list of ROMs are duplicated when binding from appsettings.json?
        //       This is a workaround to remove duplicates.
        c64HostConfig.SystemConfig.ROMs = c64HostConfig.SystemConfig.ROMs.DistinctBy(p => p.Name).ToList();

        return Task.FromResult<IHostSystemConfig>(c64HostConfig);
    }

    public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        // TODO: Should user settings be persisted? If so method GetNewHostSystemConfig() also needs to be updated to read from there instead of appsettings.json.
        return Task.CompletedTask;
    }

    public Task<ISystem> BuildSystem(string configurationVariant, IHostSystemConfig hostSystemConfig)
    {
        var c64HostSystemConfig = (C64HostConfig)hostSystemConfig;

        var c64Config = new C64Config
        {
            C64Model = configurationVariant,
            Vic2Model = C64ModelInventory.C64Models[configurationVariant].Vic2Models.First().Name, // NTSC, NTSC_old, PAL
            AudioEnabled = c64HostSystemConfig.SystemConfig.AudioEnabled,
            KeyboardJoystickEnabled = c64HostSystemConfig.SystemConfig.KeyboardJoystickEnabled,
            KeyboardJoystick = c64HostSystemConfig.SystemConfig.KeyboardJoystick,
            ROMs = c64HostSystemConfig.SystemConfig.ROMs,
            ROMDirectory = c64HostSystemConfig.SystemConfig.ROMDirectory,
        };

        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return Task.FromResult<ISystem>(c64);
    }

    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        NullRenderContext renderContext,
        NullInputHandlerContext inputHandlerContext,
        NullAudioHandlerContext audioHandlerContext
        )
    {
        var c64 = (C64)system;
        var renderer = new NullRenderer(c64);
        var inputHandler = new NullInputHandler(c64);
        var audioHandler = new NullAudioHandler(c64);
        return Task.FromResult(new SystemRunner(c64, renderer, inputHandler, audioHandler));
    }
}
