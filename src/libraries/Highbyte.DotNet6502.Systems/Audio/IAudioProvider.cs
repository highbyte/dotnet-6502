namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Marker interface for an audio provider exposed by an <see cref="ISystem"/> via
/// <see cref="ISystem.AudioProviders"/>. Used as both:
/// <list type="bullet">
/// <item>an <see cref="IAudioGenerator"/> — how the system builds its audio data;</item>
/// <item>an <see cref="IAudioSource"/> — how a host obtains that audio.</item>
/// </list>
///
/// Audio counterpart of <see cref="Rendering.IRenderProvider"/>.
/// </summary>
public interface IAudioProvider : IAudioGenerator, IAudioSource { }

/// <summary>
/// No-op audio provider, used by systems that produce no audio.
/// </summary>
public class NullAudioProvider : IAudioProvider
{
    public string Name => "NullAudioProvider";
    public void OnAfterInstruction() { }
    public void OnEndFrame() { }
}
