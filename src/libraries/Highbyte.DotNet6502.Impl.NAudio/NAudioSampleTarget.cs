using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Utils;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace Highbyte.DotNet6502.Impl.NAudio;

/// <summary>
/// NAudio host target for the PCM-sample audio style.
///
/// Plays raw PCM samples produced by an <see cref="IAudioSampleProvider"/> by adapting the
/// coordinator-supplied <see cref="AudioSampleReadCallback"/> to NAudio's <see cref="ISampleProvider"/>
/// pull contract. Desktop counterpart of a future WebAudio AudioWorklet sample target.
/// System-agnostic — knows only about float PCM samples and the host wave player.
/// </summary>
[DisplayName("NAudio PCM sample target")]
[HelpText("Plays raw PCM samples through NAudio (desktop). Pairs with the SID sample emulation provider.")]
public sealed class NAudioSampleTarget : IAudioSampleTarget, IAudioSampleTargetBufferPolicy, IInstrumentationSource
{
    public string Name => "NAudioSampleTarget";
    public Instrumentations Instrumentations { get; } = new();
    public int PrimeSilenceSamples { get; }

    private readonly NAudioAudioHandlerContext _audioHandlerContext;
    private readonly ILogger _logger;
    private readonly ElapsedMillisecondsTimedStat _readSamplesStat;
    private readonly PerSecondTimedStat _readCallbacksPerSecondStat;

    private CallbackSampleProvider? _sampleProvider;

    public NAudioSampleTarget(
        NAudioAudioHandlerContext audioHandlerContext,
        ILoggerFactory loggerFactory,
        int primeSilenceSamples = AudioSampleCoordinator.DefaultPrimeSilenceSamples)
    {
        _audioHandlerContext = audioHandlerContext;
        PrimeSilenceSamples = primeSilenceSamples;
        _logger = loggerFactory.CreateLogger(typeof(NAudioSampleTarget).Name);
        _readSamplesStat = Instrumentations.Add("ReadSamples", new ElapsedMillisecondsTimedStat());
        _readCallbacksPerSecondStat = Instrumentations.Add("CallbacksPerSecond", new PerSecondTimedStat());
    }

    public void Init(int sampleRateHz, int channelCount, AudioSampleReadCallback readSamples)
    {
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRateHz, channelCount);
        _sampleProvider = new CallbackSampleProvider(waveFormat, readSamples, _readSamplesStat, _readCallbacksPerSecondStat);

        _audioHandlerContext.ConfigureWavePlayer(_sampleProvider);
        _audioHandlerContext.StartWavePlayer();
    }

    public void StartPlaying()
    {
        _logger.LogInformation("StartPlaying called.");
        _audioHandlerContext.StartWavePlayer();
    }

    public void PausePlaying()
    {
        _logger.LogInformation("PausePlaying called.");
        _audioHandlerContext.PauseWavePlayer();
    }

    public void StopPlaying()
    {
        _logger.LogInformation("StopPlaying called.");
        _audioHandlerContext.StopWavePlayer();
    }

    public void Cleanup() => StopPlaying();

    /// <summary>
    /// Bridges NAudio's <see cref="ISampleProvider"/> (Read float[] buffer, int offset, int count)
    /// to the coordinator's <see cref="AudioSampleReadCallback"/> (which uses Span&lt;float&gt;).
    /// Underrun is filled with silence so NAudio never sees a short read — short reads cause
    /// pops/clicks in some NAudio backends.
    /// </summary>
    private sealed class CallbackSampleProvider : ISampleProvider
    {
        private readonly AudioSampleReadCallback _readSamples;
        private readonly ElapsedMillisecondsTimedStat _readSamplesStat;
        private readonly PerSecondTimedStat _readCallbacksPerSecondStat;
        public WaveFormat WaveFormat { get; }

        public CallbackSampleProvider(
            WaveFormat waveFormat,
            AudioSampleReadCallback readSamples,
            ElapsedMillisecondsTimedStat readSamplesStat,
            PerSecondTimedStat readCallbacksPerSecondStat)
        {
            WaveFormat = waveFormat;
            _readSamples = readSamples;
            _readSamplesStat = readSamplesStat;
            _readCallbacksPerSecondStat = readCallbacksPerSecondStat;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var dest = buffer.AsSpan(offset, count);
            _readCallbacksPerSecondStat.Update();
            _readSamplesStat.Start();
            int filled;
            try
            {
                filled = _readSamples(dest);
            }
            finally
            {
                _readSamplesStat.Stop();
            }
            if (filled < count)
                dest.Slice(filled).Clear(); // underrun → silence
            return count;
        }
    }
}
