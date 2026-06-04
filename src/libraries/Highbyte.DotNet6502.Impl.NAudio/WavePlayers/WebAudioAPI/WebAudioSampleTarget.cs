using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.NAudio.WavePlayers.WebAudioAPI;

/// <summary>
/// Browser WebAudio sample target that writes PCM samples directly from the emulator producer to
/// JavaScript. Unlike <see cref="WebAudioWavePlayer"/>, it does not run a timed C# playback loop.
/// </summary>
[SupportedOSPlatform("browser")]
[DisplayName("WebAudio direct PCM sample target")]
[HelpText("Plays raw PCM samples through browser WebAudio by flushing producer-written samples directly to JavaScript.")]
public sealed partial class WebAudioSampleTarget : IAudioSampleDirectWriteTarget, IInstrumentationSource, IUsesProfile
{
    public string Name => "WebAudioSampleTarget";
    public Instrumentations Instrumentations { get; } = new();

    public WavePlayerSettingsProfile ProfileType => _settings.ProfileType;

    private readonly WebAudioWavePlayerSettings _settings;
    private readonly NAudioAudioHandlerContext _audioHandlerContext;
    private readonly ILogger _logger;
    private readonly ElapsedMillisecondsTimedStat _flushSamplesStat;
    private readonly PerSecondTimedStat _flushCallbacksPerSecondStat;

    private float[] _flushBuffer = [];
    private int _flushBufferCount;
    private bool _isInitialized;

    public WebAudioSampleTarget(
        WebAudioWavePlayerSettings settings,
        NAudioAudioHandlerContext audioHandlerContext,
        ILoggerFactory loggerFactory)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _audioHandlerContext = audioHandlerContext;
        _logger = loggerFactory.CreateLogger(typeof(WebAudioSampleTarget).Name);
        _flushSamplesStat = Instrumentations.Add("FlushSamples", new ElapsedMillisecondsTimedStat());
        _flushCallbacksPerSecondStat = Instrumentations.Add("FlushCallbacksPerSecond", new PerSecondTimedStat());
    }

    public void InitDirect(int sampleRateHz, int channelCount)
    {
        _logger.LogInformation("Initializing WebAudioSampleTarget");

        if (channelCount is < 1 or > 2)
            throw new ArgumentException("WebAudioSampleTarget supports mono or stereo only", nameof(channelCount));

        _flushBuffer = new float[Math.Max(1, _settings.DirectWriteFlushSamples * channelCount)];
        _flushBufferCount = 0;

        var desiredLatencyMs = _settings.DirectWriteDesiredLatencyMs > 0
            ? _settings.DirectWriteDesiredLatencyMs
            : _settings.DesiredLatencyMs;
        var ringBufferCapacityMultiplier = _settings.DirectWriteRingBufferCapacityMultiplier > 0
            ? _settings.DirectWriteRingBufferCapacityMultiplier
            : _settings.RingBufferCapacityMultiplier;
        var minBufferBeforePlayMultiplier = _settings.DirectWriteMinBufferBeforePlayMultiplier > 0
            ? _settings.DirectWriteMinBufferBeforePlayMultiplier
            : _settings.MinBufferBeforePlayMultiplier;
        var scriptProcessorBufferSize = _settings.DirectWriteScriptProcessorBufferSize > 0
            ? _settings.DirectWriteScriptProcessorBufferSize
            : _settings.ScriptProcessorBufferSize;
        var bufferSizeSamples = (int)(sampleRateHz * (desiredLatencyMs / 1000.0));

        JSInterop.RegisterLogCallback(WebAudioWavePlayer.OnLogMessage);
        JSInterop.Initialize(
            sampleRateHz,
            channelCount,
            bufferSizeSamples,
            ringBufferCapacityMultiplier,
            minBufferBeforePlayMultiplier,
            scriptProcessorBufferSize,
            _settings.StatsIntervalMs);

        _isInitialized = true;
    }

    public int WriteSamples(ReadOnlySpan<float> samples)
    {
        if (!_isInitialized || samples.IsEmpty)
            return 0;

        var remaining = samples;
        while (!remaining.IsEmpty)
        {
            int toCopy = Math.Min(remaining.Length, _flushBuffer.Length - _flushBufferCount);
            remaining[..toCopy].CopyTo(_flushBuffer.AsSpan(_flushBufferCount));
            _flushBufferCount += toCopy;
            remaining = remaining[toCopy..];

            if (_flushBufferCount == _flushBuffer.Length)
                FlushBufferedSamples();
        }

        return samples.Length;
    }

    public void Init(int sampleRateHz, int channelCount, AudioSampleReadCallback readSamples)
    {
        throw new NotSupportedException($"{nameof(WebAudioSampleTarget)} is a direct-write sample target.");
    }

    public void StartPlaying()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Must call InitDirect first.");

        _logger.LogInformation("StartPlaying called.");
        JSInterop.Resume();
    }

    public void PausePlaying()
    {
        _logger.LogInformation("PausePlaying called.");
        FlushBufferedSamples();
        JSInterop.Pause();
    }

    public void StopPlaying()
    {
        _logger.LogInformation("StopPlaying called.");
        _flushBufferCount = 0;
        JSInterop.Stop();
    }

    public void Cleanup()
    {
        StopPlaying();
        JSInterop.Cleanup();
    }

    private void FlushBufferedSamples()
    {
        if (_flushBufferCount == 0)
            return;

        _flushCallbacksPerSecondStat.Update();
        _flushSamplesStat.Start();
        try
        {
            var samplesToSend = _flushBuffer.AsSpan(0, _flushBufferCount);
            var volume = _audioHandlerContext.MasterVolumePercent / 100f;
            var audioDataBytes = new byte[samplesToSend.Length * sizeof(float)];

            if (Math.Abs(volume - 1.0f) > 0.001f)
            {
                var scaled = new float[samplesToSend.Length];
                for (int i = 0; i < scaled.Length; i++)
                    scaled[i] = samplesToSend[i] * volume;
                Buffer.BlockCopy(scaled, 0, audioDataBytes, 0, audioDataBytes.Length);
            }
            else
            {
                Buffer.BlockCopy(_flushBuffer, 0, audioDataBytes, 0, audioDataBytes.Length);
            }

            JSInterop.QueueAudioData(audioDataBytes, samplesToSend.Length);
            _flushBufferCount = 0;
        }
        finally
        {
            _flushSamplesStat.Stop();
        }
    }

    [SupportedOSPlatform("browser")]
    private static partial class JSInterop
    {
        [JSImport("WebAudioWavePlayer.registerLogCallback", "WebAudioWavePlayer")]
        public static partial void RegisterLogCallback([JSMarshalAs<JSType.Function<JSType.Number, JSType.String>>] Action<int, string> callback);

        [JSImport("WebAudioWavePlayer.initialize", "WebAudioWavePlayer")]
        public static partial void Initialize(
            int sampleRate,
            int channels,
            int bufferSize,
            double ringBufferCapacityMultiplier,
            double minBufferBeforePlayMultiplier,
            int scriptProcessorBufferSize,
            int statsIntervalMs);

        [JSImport("WebAudioWavePlayer.queueAudioData", "WebAudioWavePlayer")]
        public static partial void QueueAudioData(byte[] audioDataBytes, int sampleCount);

        [JSImport("WebAudioWavePlayer.resume", "WebAudioWavePlayer")]
        public static partial void Resume();

        [JSImport("WebAudioWavePlayer.pause", "WebAudioWavePlayer")]
        public static partial void Pause();

        [JSImport("WebAudioWavePlayer.stop", "WebAudioWavePlayer")]
        public static partial void Stop();

        [JSImport("WebAudioWavePlayer.cleanup", "WebAudioWavePlayer")]
        public static partial void Cleanup();
    }
}
