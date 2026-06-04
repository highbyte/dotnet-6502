using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Coordinator for the PCM-sample audio style: connects an <see cref="IAudioSampleProvider"/> to
/// an <see cref="IAudioSampleTarget"/> through an SPSC ring buffer it owns. The buffer bridges
/// the emulator's bursty wall-time write cadence and the host audio device's steady drain.
///
/// Audio counterpart of <see cref="AudioCommandCoordinator"/>. Unlike the command coordinator —
/// which is a pure event forwarder with no shared state — this coordinator owns real-time-correct
/// shared state (the ring buffer). The provider and target see the buffer only as
/// <see cref="AudioSampleWriteCallback"/> and <see cref="AudioSampleReadCallback"/> respectively,
/// so neither has any dependency on the buffer class or on each other.
/// </summary>
public sealed class AudioSampleCoordinator : IAudioCoordinator, IDisposable
{
    /// <summary>
    /// Default ring buffer capacity in samples. At 44.1 kHz mono this is ~740 ms (~37 PAL or
    /// ~44 NTSC frames) of headroom — very comfortably larger than typical host audio chunk
    /// sizes (NAudio OpenAL on desktop uses 2 × 40 ms = 80 ms per pull) so timer jitter, GC
    /// pauses, and one-time JIT promotion mid-frame don't drain the buffer between producer
    /// frame ticks. Latency stays acceptable for emulator audio. Tune via the constructor
    /// overload.
    /// </summary>
    public const int DefaultRingBufferCapacitySamples = 32768;

    /// <summary>
    /// Default number of silent samples to pre-fill the ring buffer with at <see cref="Init"/>
    /// so the host audio device's first pull (which happens synchronously inside its
    /// <c>WavePlayer.Init</c>) gets a full chunk instead of underrunning before the emulator has
    /// produced its first frame. Sized to cover the OpenAL desktop default of 2 × 40 ms latency
    /// plus a generous safety margin.
    /// </summary>
    public const int DefaultPrimeSilenceSamples = 8192;

    private readonly IAudioSampleProvider _provider;
    private readonly IAudioSampleTarget _target;
    private readonly AudioSampleRingBuffer _ringBuffer;
    private readonly int _primeSilenceSamples;

    private readonly Instrumentations _instrumentations = new();
    public Instrumentations Instrumentations => _instrumentations;

    /// <summary>The ring buffer bridging producer and consumer. Exposed for diagnostics/tests.</summary>
    public AudioSampleRingBuffer RingBuffer => _ringBuffer;

    public AudioSampleCoordinator(IAudioSampleProvider provider, IAudioSampleTarget target)
        : this(provider, target, DefaultRingBufferCapacitySamples)
    {
    }

    public AudioSampleCoordinator(IAudioSampleProvider provider, IAudioSampleTarget target, int ringBufferCapacitySamples)
        : this(provider, target, ringBufferCapacitySamples, DefaultPrimeSilenceSamples)
    {
    }

    public AudioSampleCoordinator(
        IAudioSampleProvider provider,
        IAudioSampleTarget target,
        int ringBufferCapacitySamples,
        int primeSilenceSamples)
    {
        _provider = provider;
        _target = target;
        _ringBuffer = new AudioSampleRingBuffer(ringBufferCapacitySamples);
        _primeSilenceSamples = primeSilenceSamples;
    }

    public void Init()
    {
        _provider.Init(_ringBuffer.TryWrite);

        // Prime the buffer with silence before the target is initialized. Some host audio
        // backends (e.g. NAudio's OpenAL wave player) synchronously read several buffers' worth
        // of samples inside their Init call, before the emulator has produced anything. Without
        // priming, that initial pull would underrun and the silence-fill at the consumer side
        // would create an audible click on the very first frame of real audio.
        int primeCount = Math.Min(_primeSilenceSamples, _ringBuffer.Capacity);
        if (primeCount > 0)
        {
            var silence = new float[primeCount];
            _ringBuffer.TryWrite(silence);
        }

        _target.Init(_provider.SampleRateHz, _provider.ChannelCount, _ringBuffer.TryRead);
    }

    public void StartPlaying() => _target.StartPlaying();

    public void StopPlaying() => _target.StopPlaying();

    public void PausePlaying() => _target.PausePlaying();

    public void Dispose() => _target.Cleanup();

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
