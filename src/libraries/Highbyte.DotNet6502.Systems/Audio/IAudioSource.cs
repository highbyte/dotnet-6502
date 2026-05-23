namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Marker interface for the source side of an audio pipeline — how a host obtains the audio a
/// system produces.
///
/// Audio counterpart of <see cref="Rendering.IRenderSource"/>. Each audio output <em>style</em>
/// implements this interface and has different capabilities a host audio target must match:
/// <list type="bullet">
/// <item><c>IAudioCommandStream</c> — the system emits synth commands (envelope/oscillator), a
/// host synth backend (NAudio, WebAudio) generates the actual sound.</item>
/// <item><c>IAudioSampleProvider</c> — the system generates raw PCM samples itself, a host audio
/// device just plays the buffer.</item>
/// </list>
/// </summary>
public interface IAudioSource
{
    string Name { get; }
}
