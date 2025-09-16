namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
public enum BlendMode { Normal, Add, Multiply, Screen, Overlay /* extend as needed */ }

public readonly record struct LayerInfo(
    RenderSize Size,
    PixelFormat PixelFormat,
    int StrideBytes,
    float Opacity,          // 0..1
    BlendMode BlendMode,
    int ZOrder = 0          // lower draws first
                            // Optional: Matrix3x2 Transform, RectI Clip, etc.
);

/// Optional extension: provider exposes N compositing layers per frame.
public interface IVideoFrameLayerProvider : IVideoFrameProvider
{
    /// One LayerInfo per layer, stable across frames (or update on mode change).
    IReadOnlyList<LayerInfo> Layers { get; }

    /// Current front buffers for each layer (index matches Layers).
    IReadOnlyList<ReadOnlyMemory<byte>> CurrentFrontLayerBuffers { get; }
}
