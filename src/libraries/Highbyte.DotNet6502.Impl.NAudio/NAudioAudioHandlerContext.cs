using Highbyte.DotNet6502.Impl.NAudio.WavePlayers;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Highbyte.DotNet6502.Impl.NAudio;

public class NAudioAudioHandlerContext : IAudioHandlerContext
{
    private readonly IWavePlayer _wavePlayer;
    public IWavePlayer WavePlayer => _wavePlayer;

    private readonly ILogger _logger;

    private VolumeSampleProvider _masterVolumeControl = default!;

    private float _masterVolumePercent;

    public float MasterVolumePercent => _masterVolumePercent;

    public bool IsInitialized { get; private set; }

    public NAudioAudioHandlerContext(
        IWavePlayer wavePlayer,
        float initialVolumePercent,
        ILoggerFactory loggerFactory
        )
    {
        _wavePlayer = wavePlayer;
        _masterVolumePercent = initialVolumePercent;
        _logger = loggerFactory.CreateLogger(typeof(NAudioAudioHandlerContext).Name);
    }

    public static NAudioAudioHandlerContext SilentAudioHandlerContext = new NAudioAudioHandlerContext(NullWavePlayer.Instance, 0f, new NullLoggerFactory());

    public void Init()
    {
        IsInitialized = true;
    }

    public void ConfigureWavePlayer(ISampleProvider sampleProvider)
    {
        _logger.LogInformation("Configuring WavePlayer with provided SampleProvider.");

        // Route all audio through a maste volume control
        _masterVolumeControl = new VolumeSampleProvider(sampleProvider)
        {
            Volume = _masterVolumePercent / 100f
        };
        _wavePlayer.Init(_masterVolumeControl);
    }

    public void SetMasterVolumePercent(float masterVolumePercent)
    {
        _masterVolumePercent = masterVolumePercent;
        if (_masterVolumeControl != null)
            _masterVolumeControl.Volume = masterVolumePercent / 100f;
    }

    public void StartWavePlayer()
    {
        _logger.LogInformation($"Starting WavePlayer. PlaybackState before: {_wavePlayer.PlaybackState}");
        if (_wavePlayer.PlaybackState != PlaybackState.Playing)
            _wavePlayer.Play();
    }

    public void StopWavePlayer()
    {
        _logger.LogInformation($"Stopping WavePlayer. PlaybackState before: {_wavePlayer.PlaybackState}");
        if (_wavePlayer.PlaybackState != PlaybackState.Stopped)
            _wavePlayer.Stop();
    }

    public void PauseWavePlayer()
    {
        _logger.LogInformation($"Pausing WavePlayer. PlaybackState before: {_wavePlayer.PlaybackState}");
        if (_wavePlayer.PlaybackState != PlaybackState.Paused)
            _wavePlayer.Pause();
    }

    public void Cleanup()
    {
        _logger.LogInformation($"Cleaning up WavePlayer.");
        StopWavePlayer();
        _wavePlayer.Dispose();
    }
}
