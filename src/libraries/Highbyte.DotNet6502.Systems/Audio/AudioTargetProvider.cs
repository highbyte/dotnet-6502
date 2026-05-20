namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Configures and manages the audio targets available in a host application.
///
/// Audio counterpart of <see cref="Rendering.RenderTargetProvider"/>. A host registers the
/// concrete audio target types it supports; this class matches them against the audio provider
/// styles a system offers, using <see cref="AudioProviderTargetMap"/>.
///
/// Audio source/target styles are non-generic, so this is simpler than the render equivalent
/// (no open-generic matching).
/// </summary>
public class AudioTargetProvider
{
    private readonly List<(Type AudioTargetType, Func<IAudioTarget> CreateAudioTarget)> _availableAudioTargetTypes = new();

    /// <summary>
    /// Registers a concrete audio target type (e.g. a NAudio command target) and the factory used
    /// to create it.
    /// </summary>
    public void AddAudioTargetType<T>(Func<IAudioTarget> createAudioTarget) where T : IAudioTarget
    {
        _availableAudioTargetTypes.Add((typeof(T), createAudioTarget));
    }

    /// <summary>
    /// From the given concrete audio provider types, returns the subset compatible with an audio
    /// target registered in this host.
    /// </summary>
    public List<Type> GetCompatibleConcreteAudioProviderTypes(List<Type> audioProviderTypes)
    {
        var compatible = new List<Type>();
        foreach (var audioProviderType in audioProviderTypes)
        {
            var mappedTargetType = GetAudioTargetInterfaceTypeByProviderType(audioProviderType);
            if (mappedTargetType is null)
                continue;

            if (_availableAudioTargetTypes.Any(t => mappedTargetType.IsAssignableFrom(t.AudioTargetType)))
                compatible.Add(audioProviderType);
        }
        return compatible;
    }

    /// <summary>
    /// Returns the concrete audio target types in this host that can consume the given concrete
    /// audio provider type.
    /// </summary>
    public List<Type> GetConcreteAudioTargetTypesForConcreteAudioProviderType(Type concreteAudioProviderType)
    {
        var targetInterfaceType = GetAudioTargetInterfaceTypeByProviderType(concreteAudioProviderType)
            ?? throw new ArgumentException($"No audio target type is mapped for audio provider type {concreteAudioProviderType.Name}.");

        return _availableAudioTargetTypes
            .Where(t => targetInterfaceType.IsAssignableFrom(t.AudioTargetType))
            .Select(t => t.AudioTargetType)
            .ToList();
    }

    /// <summary>
    /// Creates an audio target compatible with the given concrete audio provider type. If
    /// <paramref name="concreteAudioTargetType"/> is given it must be one of the compatible types.
    /// </summary>
    public IAudioTarget CreateAudioTargetByAudioProviderType(Type concreteAudioProviderType, Type? concreteAudioTargetType = null)
    {
        var targetInterfaceType = GetAudioTargetInterfaceTypeByProviderType(concreteAudioProviderType)
            ?? throw new ArgumentException($"No audio target type is mapped for audio provider type {concreteAudioProviderType.Name}.");

        var compatible = _availableAudioTargetTypes
            .Where(t => targetInterfaceType.IsAssignableFrom(t.AudioTargetType))
            .ToList();

        if (compatible.Count == 0)
            throw new ArgumentException($"No available audio target type found that matches audio provider type {concreteAudioProviderType.Name}.");

        (Type AudioTargetType, Func<IAudioTarget> CreateAudioTarget) chosen;
        if (concreteAudioTargetType is not null)
        {
            chosen = compatible.FirstOrDefault(t => t.AudioTargetType == concreteAudioTargetType);
            if (chosen.CreateAudioTarget is null)
                throw new ArgumentException($"The specified concrete audio target type {concreteAudioTargetType.Name} is not compatible with the audio provider type {concreteAudioProviderType.Name}.");
        }
        else
        {
            chosen = compatible[0];
        }

        return chosen.CreateAudioTarget();
    }

    // Find the provider style interface (in AudioProviderTargetMap) that the concrete provider
    // type implements, then return its mapped target style interface.
    // Example: typeof(C64SidCommandStream) -> IAudioCommandStream -> IAudioCommandTarget
    private static Type? GetAudioTargetInterfaceTypeByProviderType(Type concreteAudioProviderType)
    {
        var matchedProviderInterface = AudioProviderTargetMap.Map.Keys
            .FirstOrDefault(key => key.IsAssignableFrom(concreteAudioProviderType));

        if (matchedProviderInterface is null)
            return null;

        return AudioProviderTargetMap.Map[matchedProviderInterface];
    }
}
