namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
/// <summary>
/// One complete frame worth of pixels that provides zero-copy read-only access to the source pixel data.
/// Uses ReadOnlyMemory<uint> for direct 32-bit pixel access without copying or ownership transfer.
/// The actual memory is owned by the rasterizer (e.g., Vic2Rasterizer) and protected by its locking mechanism.
/// </summary>
public sealed class RenderFrame : IAsyncDisposable
{
    public RenderSize Size { get; }
    public PixelFormat PixelFormat { get; }
    public int StrideBytes { get; }
    public TimeSpan Timestamp { get; }

    // Multi-layer path - uses ReadOnlyMemory<uint> for 32-bit pixel data
    public IReadOnlyList<LayerInfo> LayerInfos { get; }
    public IReadOnlyList<ReadOnlyMemory<uint>> LayerPixels { get; }

    // Multi-layer ctor - uses ReadOnlyMemory<uint> for pixel data
    public RenderFrame(IReadOnlyList<LayerInfo> infos, IReadOnlyList<ReadOnlyMemory<uint>> layerPixels, TimeSpan timestamp = default)
    {
        if (infos.Count != layerPixels.Count) throw new ArgumentException("infos/layerPixels length mismatch");

        // Validate that all layers have matching dimensions to prevent out-of-bounds access
        if (infos.Count > 1)
        {
            var firstSize = infos[0].Size;
            for (var i = 1; i < infos.Count; i++)
            {
                if (infos[i].Size.Width != firstSize.Width || infos[i].Size.Height != firstSize.Height)
                    throw new ArgumentException($"Layer {i} size mismatch: expected {firstSize.Width}x{firstSize.Height}, got {infos[i].Size.Width}x{infos[i].Size.Height}");
            }
        }

        LayerInfos = infos;
        LayerPixels = layerPixels;
        Timestamp = timestamp;

        // Derive overall frame "Size/Format" from the top-most (or first) layer by convention
        Size = infos.Count > 0 ? infos[0].Size : default;
        PixelFormat = infos.Count > 0 ? infos[0].PixelFormat : default;
        StrideBytes = infos.Count > 0 ? infos[0].StrideBytes : 0;
    }

    public ValueTask DisposeAsync()
    {
        // Nothing to dispose - we don't own the memory
        return ValueTask.CompletedTask;
    }

    public bool IsMultiLayer => LayerInfos.Count > 0;
}
