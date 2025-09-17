namespace Highbyte.DotNet6502.Systems.Rendering;

/// <summary>
/// Used to configure and manage the render targets use by a host application.
/// </summary>
public class RenderTargetProvider
{
    //private readonly Dictionary<Type, Func<IRenderTarget>> _availableRenderTargetTypes = new Dictionary<Type, Func<IRenderTarget>>();
    private readonly List<(Type RenderTargetType, Func<IRenderTarget> CreateRenderTarget)> _availableRenderTargetTypes = new List<(Type, Func<IRenderTarget>)>();

    // Add a concrete render target type (ex: SkiaCanvasTwoLayerRenderTarget) and the callback function that is used to create it.
    public void AddRenderTargetType<T>(Func<IRenderTarget> createRenderTarget) where T : IRenderTarget
    {
        _availableRenderTargetTypes.Add((typeof(T), createRenderTarget));
    }

    // For the provided list of concrete render provider types (ex: Vic2Rasterizer, C64VideoCommandStream, C64GpuProvider),
    // return the subset that are compatible with the available in the host app (ex: only C64VideoCommandStream in SadConsoleHostApp).
    public List<Type> GetCompatibleConcreteRenderProviderTypes(List<Type> renderProviderTypes)
    {
        // Return only sources that are compatible with the available render target types
        var compatibleRenderProviderTypes = new List<Type>();

        foreach (var renderProviderType in renderProviderTypes)
        {
            foreach (var (renderTargetType, createRenderTarget) in _availableRenderTargetTypes)
            {
                var mappedRenderTargetType = GetRenderTargetInterfaceTypeByProviderType(renderProviderType);
                if (mappedRenderTargetType is null)
                    continue; // No registered mapping for this provider type

                if (mappedRenderTargetType.IsGenericTypeDefinition)
                {
                    // Match if the concrete render target implements the open generic interface
                    if (ImplementsOpenGenericInterface(renderTargetType, mappedRenderTargetType))
                    {
                        compatibleRenderProviderTypes.Add(renderProviderType);
                        break;
                    }
                }
                else
                {
                    // Non-generic: the mapped target interface should be assignable from the concrete render target type
                    if (mappedRenderTargetType.IsAssignableFrom(renderTargetType))
                    {
                        compatibleRenderProviderTypes.Add(renderProviderType);
                        break;
                    }
                }
            }
        }

        return compatibleRenderProviderTypes;
    }

    // Find the registered provider interface (key) that this concrete type implements.
    // Example: typeof(Vic2Rasterizer) -> typeof(IVideoFrameProvider)
    private Type? GetRenderTargetInterfaceTypeByProviderType(Type concreteRenderProviderType)
    {
        // First find the matching provider interface (ex: IVideoFrameProvider) for the provided concrete type (ex: Vic2Rasterizer)
        var matchedProviderInterface = RenderProviderTargetMap.Map.Keys.FirstOrDefault(key =>
            key.IsGenericTypeDefinition
                ? ImplementsOpenGenericInterface(concreteRenderProviderType, key)
                : key.IsAssignableFrom(concreteRenderProviderType));

        if (matchedProviderInterface is null)
            return null; // No registered mapping for this provider type

        // Return the mapped target interface (ex: IRenderFrameTarget) for the matched provider interface
        // Ex: IVideoFrameProvider -> IRenderFrameTarget
        var mappedRenderTargetType = RenderProviderTargetMap.Map[matchedProviderInterface];
        return mappedRenderTargetType;
    }


    public List<Type> GetConcreteRenderTargetTypesForConcreteRenderProviderType(Type concreteRenderProviderType)
    {
        var renderTargetInterfaceType = GetRenderTargetInterfaceTypeByProviderType(concreteRenderProviderType)
            ?? throw new ArgumentException($"No Render target type is mapped for render provider type {concreteRenderProviderType.Name}.");

        var renderTargetConcreteTypes = _availableRenderTargetTypes
            .Where(tuple =>
            {
                if (renderTargetInterfaceType.IsGenericTypeDefinition)
                {
                    // For generic types, we need to ensure the generic type arguments match between provider and target
                    return HasMatchingGenericArguments(concreteRenderProviderType, tuple.RenderTargetType, renderTargetInterfaceType);
                }
                else
                {
                    // Non-generic: the mapped target interface should be assignable from the concrete render target type
                    return renderTargetInterfaceType.IsAssignableFrom(tuple.RenderTargetType);
                }
            })
            .Select(tuple => tuple.RenderTargetType)
            .ToList();
        return renderTargetConcreteTypes;
    }

    // Example:
    // - Input typeof(Vic2Rasterizer)
    // - Return SkiaCanvasTarget
    // Example:
    // - Input typeof(Vic2Rasterizer), typeof(SkiaCanvasTarget)
    // - Return SkiaCanvasTarget
    public IRenderTarget CreateRenderTargetByRenderProviderType(Type concreteRenderProviderType, Type? concretRenderTargetType = null)
    {
        var renderTargetInterfaceType = GetRenderTargetInterfaceTypeByProviderType(concreteRenderProviderType)
            ?? throw new ArgumentException($"No Render target type is mapped for render provider type {concreteRenderProviderType.Name}.");
        var renderTargetConcreteTypes = _availableRenderTargetTypes
            .Where(tuple =>
            {
                if (renderTargetInterfaceType.IsGenericTypeDefinition)
                {
                    // For generic types, we need to ensure the generic type arguments match between provider and target
                    return HasMatchingGenericArguments(concreteRenderProviderType, tuple.RenderTargetType, renderTargetInterfaceType);
                }
                else
                {
                    // Non-generic: the mapped target interface should be assignable from the concrete render target type
                    return renderTargetInterfaceType.IsAssignableFrom(tuple.RenderTargetType);
                }
            })
            .Select(tuple => tuple.RenderTargetType);

        if (!renderTargetConcreteTypes.Any())
            throw new ArgumentException($"No available render target type found that matches render provider type {concreteRenderProviderType.Name}.");

        Type renderTargetConcreteType;
        if (concretRenderTargetType is not null)
        {
            if (!renderTargetConcreteTypes.Contains(concretRenderTargetType))
                throw new ArgumentException($"The specified concrete render target type {concretRenderTargetType.Name} is not compatible with the render provider type {concreteRenderProviderType.Name}.");
            renderTargetConcreteType = concretRenderTargetType;
        }
        else
        {
            // No specific concrete target type requested, so just use the first compatible one
            renderTargetConcreteType = renderTargetConcreteTypes.First();
        }

        var targetCreator = _availableRenderTargetTypes.First(tuple => tuple.RenderTargetType == renderTargetConcreteType).CreateRenderTarget;
        return targetCreator();
    }

    private static bool ImplementsOpenGenericInterface(Type type, Type openGenericInterface)
    {
        if (!openGenericInterface.IsGenericTypeDefinition)
            return false;

        // If the type itself is a constructed version of the open generic
        if (type.IsGenericType && type.GetGenericTypeDefinition() == openGenericInterface)
            return true;

        // Or if any implemented interface is a constructed version of the open generic
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == openGenericInterface)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the generic type arguments match between a concrete render provider type and a concrete render target type.
    /// For example, if renderProvider implements IPayloadProvider&lt;PayloadC64&gt; and renderTarget implements ICustomRenderTarget&lt;PayloadC64&gt;,
    /// then they have matching generic arguments (PayloadC64).
    /// </summary>
    private static bool HasMatchingGenericArguments(Type concreteRenderProviderType, Type concreteRenderTargetType, Type renderTargetInterfaceType)
    {
        if (!renderTargetInterfaceType.IsGenericTypeDefinition)
            return false;

        // First, check if the render target implements the target interface
        if (!ImplementsOpenGenericInterface(concreteRenderTargetType, renderTargetInterfaceType))
            return false;

        // Get the corresponding provider interface from the map
        var providerInterface = RenderProviderTargetMap.Map.FirstOrDefault(kvp => kvp.Value == renderTargetInterfaceType).Key;
        if (providerInterface == null)
            return false;

        // Check if the render provider implements the provider interface
        if (!ImplementsOpenGenericInterface(concreteRenderProviderType, providerInterface))
            return false;

        // Get the generic type arguments from both interfaces
        var providerGenericArgs = GetGenericArgumentsFromInterface(concreteRenderProviderType, providerInterface);
        var targetGenericArgs = GetGenericArgumentsFromInterface(concreteRenderTargetType, renderTargetInterfaceType);

        // Check if the generic arguments match
        if (providerGenericArgs.Length != targetGenericArgs.Length)
            return false;

        for (int i = 0; i < providerGenericArgs.Length; i++)
        {
            if (providerGenericArgs[i] != targetGenericArgs[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the generic type arguments from a specific interface implemented by the given type.
    /// </summary>
    private static Type[] GetGenericArgumentsFromInterface(Type implementingType, Type openGenericInterface)
    {
        if (!openGenericInterface.IsGenericTypeDefinition)
            return Array.Empty<Type>();

        // Check if the type itself is a constructed version of the open generic
        if (implementingType.IsGenericType && implementingType.GetGenericTypeDefinition() == openGenericInterface)
            return implementingType.GetGenericArguments();

        // Check implemented interfaces
        foreach (var iface in implementingType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == openGenericInterface)
                return iface.GetGenericArguments();
        }

        return Array.Empty<Type>();
    }
}

