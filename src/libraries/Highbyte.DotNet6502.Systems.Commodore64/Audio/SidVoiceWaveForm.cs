using Highbyte.DotNet6502.Systems.Audio;

namespace Highbyte.DotNet6502.Systems.Commodore64.Audio;

/// <summary>
/// SID voice waveform selection, as encoded in the SID control register. C64-specific; mapped to
/// the system-agnostic <see cref="AudioOscillatorType"/> via <see cref="SidVoiceWaveFormExtensions"/>.
/// </summary>
public enum SidVoiceWaveForm
{
    None,
    Triangle,
    Sawtooth,
    Pulse,
    RandomNoise
}

public static class SidVoiceWaveFormExtensions
{
    /// <summary>Maps a SID waveform selection to the host-neutral oscillator type.</summary>
    public static AudioOscillatorType ToAudioOscillatorType(this SidVoiceWaveForm waveForm) => waveForm switch
    {
        SidVoiceWaveForm.None => AudioOscillatorType.None,
        SidVoiceWaveForm.Triangle => AudioOscillatorType.Triangle,
        SidVoiceWaveForm.Sawtooth => AudioOscillatorType.Sawtooth,
        SidVoiceWaveForm.Pulse => AudioOscillatorType.Pulse,
        SidVoiceWaveForm.RandomNoise => AudioOscillatorType.Noise,
        _ => AudioOscillatorType.None,
    };
}
