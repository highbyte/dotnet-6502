using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Rendering.Custom;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using System.Reflection;

namespace Highbyte.DotNet6502.Systems.Rendering;

public class RenderCoordinatorProvider
{
    private readonly IRenderLoop _renderLoop;

    public RenderCoordinatorProvider(IRenderLoop renderLoop)
    {
        _renderLoop = renderLoop;
    }

    public IRenderCoordinator CreateRenderCoordinator(
        IRenderSource renderSource,
        IRenderTarget renderTarget,
        Instrumentations instrumentations)
    {
        if (renderSource is IVideoFrameProvider videoFrameProvider && renderTarget is IRenderFrameTarget renderFrameTarget)
        {
            IFrameSource frameSource = new CommonFrameSource(videoFrameProvider);
            FrameSourceRenderCoordinator coordinator = new FrameSourceRenderCoordinator(
                frameSource,
                _renderLoop,
                renderFrameTarget);
            return coordinator;
        }
        else if (renderSource is IVideoCommandStream videoCommandStream && renderTarget is ICommandTarget commandTarget)
        {
            var coordinator = new CommandCoordinator(
                videoCommandStream,
                commandTarget,
                _renderLoop);
            return coordinator;
        }
        // Generic match: renderSource is IPayloadProvider<T> & renderTarget is ICustomRenderTarget<T> for the SAME T.
        else if (TryMatchCustomPayload(renderSource, renderTarget, out var payloadType))
        {
            var method = typeof(RenderCoordinatorProvider).GetMethod(nameof(CreateCustomPayloadCoordinator), BindingFlags.NonPublic | BindingFlags.Instance)!;
            var genericMethod = method.MakeGenericMethod(payloadType);
            var coordinator = genericMethod.Invoke(this, new object[] { renderSource, renderTarget })
                ?? throw new InvalidOperationException("Failed to create custom payload coordinator.");
            return (IRenderCoordinator)coordinator;
        }
        else
        {
            throw new ArgumentException($"Render source type {renderSource.GetType().Name} and target type {renderTarget.GetType().Name} is not supported.");
        }
    }

    // Attempts to find a matching generic argument T where renderSource implements IPayloadProvider<T>
    // and renderTarget implements ICustomRenderTarget<T> (same T). Returns true and the payload type if found.
    private static bool TryMatchCustomPayload(IRenderSource renderSource, IRenderTarget renderTarget, out Type payloadType)
    {
        payloadType = null!;

        var sourceInterfaces = renderSource.GetType().GetInterfaces();
        var targetInterfaces = renderTarget.GetType().GetInterfaces();

        foreach (var sIface in sourceInterfaces)
        {
            if (!sIface.IsGenericType) continue;
            if (sIface.GetGenericTypeDefinition() != typeof(IPayloadProvider<>)) continue;
            var candidatePayloadType = sIface.GetGenericArguments()[0];
            if (!typeof(IRenderPayload).IsAssignableFrom(candidatePayloadType)) continue;

            // Does target have ICustomRenderTarget<candidatePayloadType> ?
            var wantedTargetInterface = typeof(ICustomRenderTarget<>).MakeGenericType(candidatePayloadType);
            if (targetInterfaces.Any(t => t == wantedTargetInterface))
            {
                payloadType = candidatePayloadType;
                return true;
            }
        }
        return false;
    }

    private object CreateCustomPayloadCoordinator<T>(
        IPayloadProvider<T> renderSource,
        ICustomRenderTarget<T> renderTarget) where T : IRenderPayload
    {
        var coordinator = new PayloadCoordinator<T>(
            renderSource,
            _renderLoop,
            renderTarget);

        return coordinator;
    }
}

