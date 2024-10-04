using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.Commodore64.Audio;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Input;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Video;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class C64Setup : ISystemConfigurer<SadConsoleRenderContext, SadConsoleInputHandlerContext, NAudioAudioHandlerContext>
{
    public string SystemName => C64.SystemName;

    public Task<List<string>> GetConfigurationVariants(IHostSystemConfig hostSystemConfig) => Task.FromResult(s_systemVariants);
    public List<string> ConfigurationVariants => s_systemVariants;

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

        // TODO: Why is list of ROMs are duplicated when binding from appsettings.json?
        //       This is a workaround to remove duplicates.
        c64HostConfig.SystemConfig.ROMs = c64HostConfig.SystemConfig.ROMs.DistinctBy(p => p.Name).ToList();

        // TODO: Code suggestion AI backend type should not be set in system specific config.
        //       For now workaround by reading from a common setting.
        c64HostConfig.CodeSuggestionBackendType = Enum.Parse<CodeSuggestionBackendTypeEnum>(_configuration["CodingAssistant:CodingAssistantType"] ?? "None");

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
            ROMs = c64HostSystemConfig.SystemConfig.ROMs,
            ROMDirectory = c64HostSystemConfig.SystemConfig.ROMDirectory,
        };

        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return Task.FromResult<ISystem>(c64);
    }

    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        SadConsoleRenderContext renderContext,
        SadConsoleInputHandlerContext inputHandlerContext,
        NAudioAudioHandlerContext audioHandlerContext
        )
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        var c64 = (C64)system;

        var renderer = new C64SadConsoleRenderer(c64, renderContext);

        ICodeSuggestion codeSuggestion = CodeSuggestionConfigurator.CreateCodeSuggestion(c64HostConfig.CodeSuggestionBackendType, _configuration, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION, C64BasicCodingAssistant.CODE_COMPLETION_EXAMPLE_MESSAGES, defaultToNoneIdConfigError: true);
        var c64BasicCodingAssistant = new C64BasicCodingAssistant(c64, codeSuggestion, _loggerFactory);
        var inputHandler = new C64SadConsoleInputHandler(c64, inputHandlerContext, _loggerFactory, c64BasicCodingAssistant, c64HostConfig.BasicAIAssistantDefaultEnabled);

        var audioHandler = new C64NAudioAudioHandler(c64, audioHandlerContext, _loggerFactory);

        return Task.FromResult(new SystemRunner(c64, renderer, inputHandler, audioHandler));
    }
}
