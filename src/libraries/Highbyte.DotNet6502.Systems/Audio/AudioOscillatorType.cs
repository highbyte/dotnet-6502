namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Oscillator (waveform) type for a synth voice in the command-stream audio style.
///
/// System-agnostic: the C64 SID, the AY-3-8910 PSG and the NES APU pulse channels all map onto
/// this set. A system's audio source decodes its native waveform encoding into this enum; a host
/// <see cref="IAudioCommandTarget"/> selects the matching oscillator implementation.
/// </summary>
public enum AudioOscillatorType
{
    None,
    Triangle,
    Sawtooth,
    Pulse,
    Noise
}
