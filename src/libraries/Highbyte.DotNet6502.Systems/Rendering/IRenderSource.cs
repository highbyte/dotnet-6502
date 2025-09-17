
namespace Highbyte.DotNet6502.Systems.Rendering;

/// <summary>
/// Marker interface used as RenderSource member in ISystem interface to present a source of how to get a rendered frame.
/// Each style of rendering (IVideoFrameProvider, IVideoCommandStream, and IPayloadProvider) implements this interface, and has different capabilities that must be taken into account.
/// </summary>
public interface IRenderSource
{
    public string Name { get; }
}
