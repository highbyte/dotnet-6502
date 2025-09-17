using Highbyte.DotNet6502.Systems.Rendering.Custom;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;

namespace Highbyte.DotNet6502.Systems.Rendering;

public static class RenderProviderTargetMap
{
    // Map of what the corresponding general render target type is for each general render provider type.
    public static readonly Dictionary<Type, Type> Map = new Dictionary<Type, Type>()
    {
        { typeof(IVideoFrameProvider), typeof(IRenderFrameTarget) },
        { typeof(IVideoCommandStream), typeof(ICommandTarget) },
        { typeof(IPayloadProvider<>), typeof(ICustomRenderTarget<>)}
    };
}
