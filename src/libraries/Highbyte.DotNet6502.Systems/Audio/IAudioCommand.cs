namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Marker interface for a single audio command in the command-stream audio style — an instruction
/// telling a host synth backend what to do (start a note, change frequency, set volume, ...).
///
/// Audio counterpart of <see cref="Rendering.VideoCommands.IVideoCommand"/>. The concrete command
/// records (<see cref="SetVolumeAudioCommand"/>, <see cref="VoiceAudioCommand"/>) are a
/// system-agnostic synth-voice vocabulary in core — any voice-based sound chip can emit them.
/// </summary>
public interface IAudioCommand { }
