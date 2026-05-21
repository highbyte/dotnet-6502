namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// The system-agnostic synth-voice command vocabulary for the command-stream audio style. A
/// system's <see cref="IAudioCommandStream"/> emits these; a host <see cref="IAudioCommandTarget"/>
/// executes them against its synth backend (NAudio, WebAudio).
///
/// Audio counterpart of the render pipeline's <see cref="Rendering.VideoCommands.IVideoCommand"/>
/// vocabulary — generic, in core, reusable by any voice-based sound chip.
/// </summary>

/// <summary>Set the global synth volume.</summary>
public sealed record SetVolumeAudioCommand(float Gain) : IAudioCommand;

/// <summary>
/// A per-voice synth action. The specific action (start ADS, start release, change frequency,
/// change pulse width, stop) is carried in <see cref="AudioVoiceParameter.AudioCommand"/>.
/// </summary>
public sealed record VoiceAudioCommand(byte Voice, AudioVoiceParameter Parameter) : IAudioCommand;
