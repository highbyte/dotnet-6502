namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Maps each audio <em>source</em> style interface to the host audio <em>target</em> style
/// interface that can consume it.
///
/// Audio counterpart of <see cref="Rendering.RenderProviderTargetMap"/>. Populated as styles are
/// added — phase 2 adds the command-stream style
/// (<c>IAudioCommandStream</c> → <c>IAudioCommandTarget</c>); a future PCM-sample style adds
/// <c>IAudioSampleProvider</c> → <c>IAudioSampleTarget</c>.
/// </summary>
public static class AudioProviderTargetMap
{
    public static readonly Dictionary<Type, Type> Map = new Dictionary<Type, Type>()
    {
        { typeof(IAudioCommandStream), typeof(IAudioCommandTarget) },
        // A future PCM-sample style adds: IAudioSampleProvider -> IAudioSampleTarget
    };
}
