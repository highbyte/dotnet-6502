namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Callback the producer (sample provider) uses to push freshly generated PCM samples into the
/// coordinator's ring buffer.
///
/// Defined as a named delegate because <see cref="Span{T}"/> is a ref struct and cannot be used as
/// a type argument to <see cref="Func{T, TResult}"/>.
/// </summary>
/// <param name="samples">Interleaved float samples to enqueue (channel order
/// frame0-ch0, frame0-ch1, frame1-ch0, ...).</param>
/// <returns>Number of samples actually accepted by the buffer. Less than <c>samples.Length</c>
/// indicates the producer is outrunning the consumer (overrun); excess samples are dropped.</returns>
public delegate int AudioSampleWriteCallback(ReadOnlySpan<float> samples);

/// <summary>
/// The PCM-sample audio source style: the system generates raw PCM samples itself and the host
/// audio device just plays them.
///
/// Audio counterpart of the render-side pixel buffer styles. Unlike
/// <see cref="IAudioCommandStream"/> — which is push event-by-event for individual synth commands —
/// this style is also push but in bulk PCM: as the emulator ticks (via the provider's
/// <see cref="IAudioGenerator.OnAfterInstruction"/> implementation) the provider writes any new
/// samples into the coordinator-owned ring buffer through the <see cref="AudioSampleWriteCallback"/>
/// supplied to <see cref="Init"/>. The buffer bridges the emulator's bursty wall-time cadence
/// and the host audio device's steady drain.
///
/// The provider itself owns no buffer state — only SID-core state — which keeps it
/// synchronously testable: feed it cycles, observe the samples handed to a stub callback.
/// </summary>
public interface IAudioSampleProvider : IAudioSource
{
    /// <summary>Output sample rate in Hz (e.g. 44100, 48000).</summary>
    int SampleRateHz { get; }

    /// <summary>Number of interleaved channels per frame (1 = mono; the SID is mono).</summary>
    int ChannelCount { get; }

    /// <summary>
    /// Wires the provider to the coordinator-owned ring buffer by supplying the write callback the
    /// provider invokes whenever its sample-generation step (driven by
    /// <see cref="IAudioGenerator.OnAfterInstruction"/>) produces new samples. Called once before
    /// playback begins.
    /// </summary>
    void Init(AudioSampleWriteCallback writeSamples);
}
