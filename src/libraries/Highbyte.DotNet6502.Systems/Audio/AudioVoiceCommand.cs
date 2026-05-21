namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// The per-voice action carried by a <see cref="VoiceAudioCommand"/> in the command-stream audio
/// style — what a host synth backend should do with the voice.
///
/// System-agnostic: a system's audio source decodes its native sound-chip state into these
/// actions (e.g. the C64 decodes SID gate-register transitions).
/// </summary>
public enum AudioVoiceCommand
{
    None,
    StartADS,           // Start attack/decay/sustain cycle.
    StartRelease,       // Start release cycle, which will fade volume down to 0 during the release period.
    ChangeFrequency,    // Change frequency on current playing audio.
    ChangePulseWidth,   // Change pulse width on current playing audio (only for pulse oscillator).
    Stop                // Stop current playing audio right away.
}
