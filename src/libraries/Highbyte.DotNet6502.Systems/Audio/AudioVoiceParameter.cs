namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// A system-agnostic synth-voice description for the command-stream audio style: what a host
/// synth backend needs to play one voice (oscillator type, frequency, pulse width, ADSR).
///
/// Carried by <see cref="VoiceAudioCommand"/>. A system's audio source builds this by decoding
/// its native sound chip — e.g. the C64 builds it from SID register values (frequency in Hz,
/// ADSR in seconds, normalized pulse width). The values are already in host-neutral units, so
/// any host <see cref="IAudioCommandTarget"/> can consume them without system knowledge.
/// </summary>
public class AudioVoiceParameter
{
    /// <summary>What to do with the voice (start ADS, start release, change frequency, ...).</summary>
    public AudioVoiceCommand AudioCommand { get; set; }

    /// <summary>The oscillator (waveform) the voice should use.</summary>
    public AudioOscillatorType OscillatorType { get; set; }

    /// <summary>Frequency in Hz.</summary>
    public float Frequency { get; set; }

    /// <summary>Pulse width, normalized to −1..+1 (only meaningful for the pulse oscillator).</summary>
    public float PulseWidth { get; set; }

    /// <summary>Attack duration in seconds.</summary>
    public double AttackDurationSeconds { get; set; }

    /// <summary>Decay duration in seconds.</summary>
    public double DecayDurationSeconds { get; set; }

    /// <summary>Sustain level, 0.0–1.0.</summary>
    public float SustainGain { get; set; }

    /// <summary>Release duration in seconds.</summary>
    public double ReleaseDurationSeconds { get; set; }
}
