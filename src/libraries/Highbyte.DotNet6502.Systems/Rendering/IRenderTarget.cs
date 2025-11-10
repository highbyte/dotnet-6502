namespace Highbyte.DotNet6502.Systems.Rendering;

/// <summary>
/// Marker interface used as the target of a rendering pipeline.
/// Each style of target (IRenderFrameTarget, ICommandTarget, and ICustomRenderTarget) implements this interface, and has different capabilities that must be taken into account.
/// </summary>
public interface IRenderTarget
{
    public string Name { get; }
}
