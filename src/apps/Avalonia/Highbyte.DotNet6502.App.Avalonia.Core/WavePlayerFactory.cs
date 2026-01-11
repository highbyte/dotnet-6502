using System;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers.SilkNetOpenAL;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers.WebAudioAPI;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

public class WavePlayerFactory
{
    private readonly ILogger<WavePlayerFactory> _logger;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly ILoggerFactory _loggerFactory;
    public WavePlayerFactory(ILoggerFactory loggerFactory, EmulatorConfig emulatorConfig)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<WavePlayerFactory>();
        _emulatorConfig = emulatorConfig;
    }
    public IWavePlayer CreateWavePlayer()
    {
        IWavePlayer? wavePlayer;
        if (PlatformDetection.IsRunningOnDesktop())
        {
            _logger.LogInformation("Creating NAudio SilkNetOpenALWavePlayer for desktop cross-platform");

            // Create Naudio wave player for desktop (using cross-platform OpenAL WavePlayer)
            wavePlayer = new SilkNetOpenALWavePlayer()
            {
                NumberOfBuffers = 2,
                DesiredLatency = 40
            };
        }
        else if (PlatformDetection.IsRunningInWebAssembly())
        {
            var profile = _emulatorConfig.AudioSettingsProfile;
            _logger.LogInformation($"Creating NAudio WebAudioWavePlayer for browser platform with profile: {profile}");

            // Create NAudio WavePlayer for browser (using WebAudio API JavaScript interop)
            wavePlayer = new WebAudioWavePlayer(WebAudioWavePlayerSettings.GetSettingsForProfile(profile), _loggerFactory);

            _logger.LogInformation("WebAudioWavePlayer created");

            // Init capture of WebAudioWavePlayer.js JS logging to the .NET side (static JSExport interop method)
            WebAudioWavePlayer.SetLogger(_loggerFactory.CreateLogger(typeof(WebAudioWavePlayer).Name));

            _logger.LogInformation("WebAudioWavePlayer logger set");
        }
        else
        {
            throw new NotSupportedException("No suitable audio output available for the current platform.");
        }
        return wavePlayer;
    }
}