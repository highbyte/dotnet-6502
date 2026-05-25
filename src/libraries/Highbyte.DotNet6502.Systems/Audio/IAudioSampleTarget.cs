namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Callback the consumer (host audio target) uses to pull PCM samples from the coordinator's
/// ring buffer when the host audio device needs more data.
///
/// Defined as a named delegate because <see cref="Span{T}"/> is a ref struct and cannot be used as
/// a type argument to <see cref="Func{T, TResult}"/>.
/// </summary>
/// <param name="destination">Interleaved float buffer to fill.</param>
/// <returns>Number of samples actually read. May be less than <c>destination.Length</c> on
/// underrun; the unfilled tail should be treated as silence.</returns>
public delegate int AudioSampleReadCallback(Span<float> destination);

/// <summary>
/// The PCM-sample audio target style: a host audio backend (NAudio, WebAudio AudioWorklet, ...)
/// that plays raw PCM samples produced by an <see cref="IAudioSampleProvider"/>.
///
/// Audio counterpart of <see cref="IAudioCommandTarget"/> but for the pull-based sample style:
/// the host audio device runs on its own clock and asks for samples on demand via the
/// <see cref="AudioSampleReadCallback"/> supplied to <see cref="Init"/>. The target sees the
/// coordinator's ring buffer only through the callback — it has no reference to the provider or
/// the buffer itself. Implemented per host technology.
/// </summary>
public interface IAudioSampleTarget : IAudioTarget
{
    /// <summary>
    /// Sets up the host audio backend. Called once before playback.
    /// </summary>
    /// <param name="sampleRateHz">Sample rate the provider generates at (e.g. 44100).</param>
    /// <param name="channelCount">Interleaved channel count (1 = mono).</param>
    /// <param name="readSamples">Pull callback the target invokes from its audio thread when the
    /// device needs more data.</param>
    void Init(int sampleRateHz, int channelCount, AudioSampleReadCallback readSamples);

    /// <summary>Starts (or resumes) audio output.</summary>
    void StartPlaying();

    /// <summary>Stops audio output and silences the device.</summary>
    void StopPlaying();

    /// <summary>Pauses audio output without tearing down the backend.</summary>
    void PausePlaying();

    /// <summary>Tears down the host audio backend.</summary>
    void Cleanup();
}
