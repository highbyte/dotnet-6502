namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Maps each audio <em>source</em> style interface to the host audio <em>target</em> style
/// interface that can consume it.
///
/// Audio counterpart of <see cref="Rendering.RenderProviderTargetMap"/>. Populated as styles are
/// added — the command-stream style (<c>IAudioCommandStream</c> → <c>IAudioCommandTarget</c>) and
/// the PCM-sample style (<c>IAudioSampleProvider</c> → <c>IAudioSampleTarget</c>).
/// </summary>
public static class AudioProviderTargetMap
{
    public static readonly Dictionary<Type, Type> Map = new Dictionary<Type, Type>()
    {
        { typeof(IAudioCommandStream), typeof(IAudioCommandTarget) },
        { typeof(IAudioSampleProvider), typeof(IAudioSampleTarget) },
    };
}
