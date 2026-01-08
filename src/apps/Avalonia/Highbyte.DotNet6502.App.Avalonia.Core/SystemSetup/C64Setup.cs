using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Impl.Avalonia.Commodore64.Input;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.Commodore64.Audio;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

public class C64Setup : ISystemConfigurer<AvaloniaInputHandlerContext, NAudioAudioHandlerContext>
{
    public string SystemName => C64.SystemName;

    private readonly Func<string, string, string?, Task>? _saveCustomConfigString = null;

    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig) => Task.FromResult(s_systemVariants);

    private static readonly List<string> s_systemVariants = C64ModelInventory.C64Models.Keys.ToList();

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<C64Setup> _logger;
    private readonly IConfiguration _configuration;

    public C64Setup(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        Func<string, string, string?, Task>? saveCustomConfigString = null)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<C64Setup>();
        _configuration = configuration;
        _saveCustomConfigString = saveCustomConfigString;
    }

    public async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var c64HostConfig = new C64HostConfig();
        _logger.LogInformation("Loading C64HostConfig from IConfiguration.");
        _configuration.GetSection($"{C64HostConfig.ConfigSectionName}").Bind(c64HostConfig);
        _logger.LogInformation("Successfully loaded C64HostConfig from IConfiguration.");

        return c64HostConfig;
    }

    public async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (_saveCustomConfigString == null)
        {
            _logger.LogWarning("No method for saving custom config JSON supplied, so not saving C64HostConfig.");
            return;
        }
        var json = JsonSerializer.Serialize(hostSystemConfig, HostConfigJsonContext.Default.C64HostConfig);
        await _saveCustomConfigString(C64HostConfig.ConfigSectionName, json, null);
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

    public async Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        AvaloniaInputHandlerContext inputHandlerContext,
        NAudioAudioHandlerContext audioHandlerContext
        )
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        var c64 = (C64)system;

        ICodeSuggestion codeSuggestion;
        C64BasicCodingAssistant c64BasicCodingAssistant;
        codeSuggestion = CodeSuggestionConfigurator.CreateCodeSuggestion(c64HostConfig.CodeSuggestionBackendType, _configuration, _loggerFactory, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION, C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION, defaultToNoneIdConfigError: true);
        c64BasicCodingAssistant = new C64BasicCodingAssistant(c64, codeSuggestion, _loggerFactory);

        var inputHandler = new AvaloniaC64InputHandler(c64, inputHandlerContext, _loggerFactory, c64HostConfig.InputConfig, c64BasicCodingAssistant, c64HostConfig.BasicAIAssistantDefaultEnabled);

        // Use NAudio (cross platform synth waveforms generation) for desktop and browser platforms, NullAudioHandler for others.
        // Note: It's inside NAudioAudioHandlerContext that has dependency to specific IWavePlayer implementation for either Desktop (SilkNetOpenAL) or Browser (WebAudio) that actually playes the generated samples.
        IAudioHandler audioHandler;
        if (hostSystemConfig.SystemConfig.AudioEnabled &&
            (PlatformDetection.IsRunningOnDesktop() || PlatformDetection.IsRunningInWebAssembly()))
        {
            audioHandler = new C64NAudioAudioHandler(c64, audioHandlerContext, _loggerFactory);
        }
        else
        {
            audioHandler = new NullAudioHandler(c64);
        }

        return new SystemRunner(c64, inputHandler, audioHandler);
    }
}
