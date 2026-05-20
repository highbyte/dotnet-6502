namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Creates the <see cref="IAudioCoordinator"/> that connects a system's <see cref="IAudioSource"/>
/// to a host's <see cref="IAudioTarget"/>.
///
/// Audio counterpart of <see cref="Rendering.RenderCoordinatorProvider"/>.
/// </summary>
public class AudioCoordinatorProvider
{
    public IAudioCoordinator CreateAudioCoordinator(IAudioSource audioSource, IAudioTarget audioTarget)
    {
        if (audioSource is IAudioCommandStream commandStream && audioTarget is IAudioCommandTarget commandTarget)
        {
            return new AudioCommandCoordinator(commandStream, commandTarget);
        }

        // A future PCM-sample style adds an IAudioSampleProvider / IAudioSampleTarget branch here.

        throw new ArgumentException(
            $"Audio source type {audioSource.GetType().Name} and target type {audioTarget.GetType().Name} is not a supported combination.");
    }
}
