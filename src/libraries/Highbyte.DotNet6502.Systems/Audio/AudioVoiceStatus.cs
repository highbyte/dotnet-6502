namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// ADSR lifecycle state of a synth voice in the command-stream audio style. System-agnostic.
/// </summary>
public enum AudioVoiceStatus
{
    /// <summary>
    /// Attack/Decay/Sustain cycle has been started.
    /// On the C64 it is started by setting the SID Gate bit to 1 (with a waveform selected).
    /// </summary>
    ADSCycleStarted,

    /// <summary>
    /// Release cycle has been started.
    /// On the C64 it is started by setting the SID Gate bit to 0.
    /// During the release cycle a new audio can be started (this stops the current sound and starts a new one).
    /// </summary>
    ReleaseCycleStarted,

    /// <summary>
    /// The audio has stopped playing — either the release cycle completed, or the audio was
    /// stopped outright (on the C64, by clearing all waveform selection bits).
    /// </summary>
    Stopped
}
