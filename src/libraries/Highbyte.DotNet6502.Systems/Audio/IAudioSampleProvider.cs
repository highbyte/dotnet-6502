namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Callback the consumer (host audio device) uses to pull PCM samples from a
/// <see cref="IAudioSampleProvider"/>.
///
/// Defined as a named delegate because <see cref="Span{T}"/> is a ref struct and cannot be used as
/// a type argument to <see cref="Func{T, TResult}"/>.
/// </summary>
/// <param name="destination">Interleaved float buffer to fill (channel order
/// frame0-ch0, frame0-ch1, frame1-ch0, ...).</param>
/// <returns>Number of <em>frames</em> (one frame = ChannelCount samples) actually written.
/// May be less than the buffer capacity when the producer has not yet generated enough samples
/// (underrun); callers should treat the unfilled tail as silence.</returns>
public delegate int AudioSampleReadCallback(Span<float> destination);

/// <summary>
/// The PCM-sample audio source style: the system generates raw PCM samples itself and the host
/// audio device just plays them.
///
/// Audio counterpart of the render-side pixel buffer styles. Unlike
/// <see cref="IAudioCommandStream"/> — which is push, with the system emitting synth commands —
/// this style is <em>pull</em>: the host audio device drives the cadence by asking for the next
/// N samples whenever its DAC is hungry. A ring buffer inside the provider bridges the emulator
/// clock (bursty in wall time) and the host audio device clock (steady drain at the sample rate).
///
/// Producer side: the provider is also an <see cref="IAudioGenerator"/>, so the emulator ticks
/// the SID core during <see cref="IAudioGenerator.OnAfterInstruction"/> and accumulates samples
/// into the internal ring buffer. Consumer side: <see cref="ReadSamples"/> drains them on the
/// host audio thread.
/// </summary>
public interface IAudioSampleProvider : IAudioSource
{
    /// <summary>Output sample rate in Hz (e.g. 44100, 48000).</summary>
    int SampleRateHz { get; }

    /// <summary>Number of interleaved channels per frame (1 = mono; the SID is mono).</summary>
    int ChannelCount { get; }

    /// <summary>
    /// Pull samples from the provider's internal ring buffer. Called by the host audio target
    /// (via the coordinator wiring) when the audio device needs more data.
    /// </summary>
    /// <param name="destination">Interleaved float buffer to fill.</param>
    /// <returns>Number of frames actually written. May be less than the buffer capacity on
    /// underrun; the unfilled tail should be treated as silence.</returns>
    int ReadSamples(Span<float> destination);
}
