namespace Highbyte.DotNet6502.Systems.Rendering;

/// <summary>
/// Marker interface used as both:
/// - RenderGenerator member in ISystem interface to present a source of how a frame is constructed
/// - RenderSource member in ISystem interface to present a source of how to get a rendered frame (consumed by a target)
/// </summary>
public interface IRenderProvider : IRenderGenerator, IRenderSource { }

public class NullRenderProvider : IRenderProvider
{
    public string Name => "NullRenderProvider";
    public void OnAfterInstruction()
    {
        // Do nothing
    }
    public void OnEndFrame()
    {
        // Do nothing
    }
}
