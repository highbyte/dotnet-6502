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
    /// Default ring buffer capacity in samples. At 44.1 kHz mono this is ~93 ms (~4–5 PAL frames)
    /// of headroom — enough to absorb GC pauses and scheduling jitter while keeping latency
    /// comparable to the existing command-stream path. Tune via the constructor overload.
    /// </summary>
    public const int DefaultRingBufferCapacitySamples = 4096;

    private readonly IAudioSampleProvider _provider;
    private readonly IAudioSampleTarget _target;
    private readonly AudioSampleRingBuffer _ringBuffer;

    private readonly Instrumentations _instrumentations = new();
    public Instrumentations Instrumentations => _instrumentations;

    /// <summary>The ring buffer bridging producer and consumer. Exposed for diagnostics/tests.</summary>
    public AudioSampleRingBuffer RingBuffer => _ringBuffer;

    public AudioSampleCoordinator(IAudioSampleProvider provider, IAudioSampleTarget target)
        : this(provider, target, DefaultRingBufferCapacitySamples)
    {
    }

    public AudioSampleCoordinator(IAudioSampleProvider provider, IAudioSampleTarget target, int ringBufferCapacitySamples)
    {
        _provider = provider;
        _target = target;
        _ringBuffer = new AudioSampleRingBuffer(ringBufferCapacitySamples);
    }

    public void Init()
    {
        _provider.Init(_ringBuffer.TryWrite);
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
