using Highbyte.DotNet6502.App.SadConsole.CodingAssistant.Inference;
using Highbyte.DotNet6502.App.SadConsole.CodingAssistant.Inference.OpenAI;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.Commodore64.Audio;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Input;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Video;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class C64Setup : ISystemConfigurer<SadConsoleRenderContext, SadConsoleInputHandlerContext, NAudioAudioHandlerContext>
{
    public string SystemName => C64.SystemName;
    public List<string> ConfigurationVariants => s_systemVariants;

    private static readonly List<string> s_systemVariants = C64ModelInventory.C64Models.Keys.ToList();


    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;


    private readonly OpenAIInferenceBackend _inferenceBackend;
    private readonly CodeCompletionInference _codeCompletionInference;
    private readonly CodeCompletionConfig _codeCompletionConfig;

    public C64Setup(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;

        _inferenceBackend = new OpenAIInferenceBackend(configuration);
        _codeCompletionConfig = new CodeCompletionConfig();
        _codeCompletionInference = new CodeCompletionInference();
    }

    public IHostSystemConfig GetNewHostSystemConfig()
    {
        // TODO: Read System host config from appsettings.json
        var c64HostConfig = new C64HostConfig { };
        return c64HostConfig;
    }

    public Task<ISystemConfig> GetNewConfig(string configurationVariant)
    {
        if (!s_systemVariants.Contains(configurationVariant))
            throw new ArgumentException($"Unknown configuration variant '{configurationVariant}'.");

        var c64Config = new C64Config() { ROMs = new() };
        _configuration.GetSection($"{C64Config.ConfigSectionName}.{configurationVariant}").Bind(c64Config);
        c64Config.SetROMDefaultChecksums();
        return Task.FromResult<ISystemConfig>(c64Config);
    }

    public Task PersistConfig(ISystemConfig systemConfig)
    {
        var c64Config = (C64Config)systemConfig;
        // TODO: Persist settings to file

        return Task.CompletedTask;
    }

    public ISystem BuildSystem(ISystemConfig systemConfig)
    {
        var c64Config = (C64Config)systemConfig;
        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return c64;
    }

    public SystemRunner BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        IHostSystemConfig hostSystemConfig,
        SadConsoleRenderContext renderContext,
        SadConsoleInputHandlerContext inputHandlerContext,
        NAudioAudioHandlerContext audioHandlerContext
        )
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        var c64 = (C64)system;

        var renderer = new C64SadConsoleRenderer(c64, renderContext);

        c64HostConfig.BasicAIAssistantEnabled = c64HostConfig.BasicAIAssistantDefaultEnabled;
        Func<bool> getCodeCompletionEnabled = () => c64HostConfig.BasicAIAssistantEnabled && _inferenceBackend.IsAvailable;
        Func<string, string, Task<string>>? getCodeCompetion = _inferenceBackend.IsAvailable ? GetCodeCompletionAsync : null;
        var inputHandler = new C64SadConsoleInputHandler(c64, inputHandlerContext, _loggerFactory, getCodeCompletionEnabled, getCodeCompetion);

        var audioHandler = new C64NAudioAudioHandler(c64, audioHandlerContext, _loggerFactory);


        return new SystemRunner(c64, renderer, inputHandler, audioHandler);

    }

    private async Task<string> GetCodeCompletionAsync(string textBefore, string textAfter)
    {
        return await _codeCompletionInference.GetInsertionSuggestionAsync(_inferenceBackend, _codeCompletionConfig, textBefore, textAfter);
    }
}
