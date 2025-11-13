using System.Buffers;

namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
/// <summary>
/// One complete frame worth of pixels. Owns the backing memory (via IMemoryOwner),
/// but exposes a Memory<uint> for zero-copy handoff to renderers.
/// Uses uint[] for direct 32-bit pixel access without conversion overhead.
/// </summary>
public sealed class RenderFrame : IAsyncDisposable
{
    public RenderSize Size { get; }
    public PixelFormat PixelFormat { get; }
    public int StrideBytes { get; }
    public TimeSpan Timestamp { get; }

    // Single-buffer path (legacy/simple)
    public IMemoryOwner<byte>? Owner { get; }
    public Memory<byte> Pixels { get; }

    // Multi-layer path (advanced) - uses uint for 32-bit pixel data
    public IReadOnlyList<LayerInfo> LayerInfos { get; }
    public IReadOnlyList<IMemoryOwner<uint>> LayerOwners { get; }
    public IReadOnlyList<Memory<uint>> LayerPixels { get; }

    // Single-buffer ctor
    public RenderFrame(RenderSize size, PixelFormat fmt, IMemoryOwner<byte> owner, TimeSpan timestamp = default)
    {
        Size = size; PixelFormat = fmt; Timestamp = timestamp;
        StrideBytes = size.StrideBytes(fmt);
        Owner = owner;
        Pixels = owner.Memory[..(StrideBytes * size.Height)];
        LayerInfos = Array.Empty<LayerInfo>();
        LayerOwners = Array.Empty<IMemoryOwner<uint>>();
        LayerPixels = Array.Empty<Memory<uint>>();
    }

    // Multi-layer ctor - uses uint[] for pixel data
    public RenderFrame(IReadOnlyList<LayerInfo> infos, IReadOnlyList<IMemoryOwner<uint>> owners, TimeSpan timestamp = default)
    {
        if (infos.Count != owners.Count) throw new ArgumentException("infos/owners length mismatch");

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

        LayerInfos = infos; LayerOwners = owners; Timestamp = timestamp;

        // Derive overall frame “Size/Format” from the top-most (or first) layer by convention
        Size = infos.Count > 0 ? infos[0].Size : default;
        PixelFormat = infos.Count > 0 ? infos[0].PixelFormat : default;
        StrideBytes = infos.Count > 0 ? infos[0].StrideBytes : 0;

        Owner = null; Pixels = Memory<byte>.Empty;
        var layerPixels = new Memory<uint>[owners.Count];
        var pixelCount = infos.Count > 0 ? infos[0].Size.Width * infos[0].Size.Height : 0;
        for (var i = 0; i < owners.Count; i++)
            layerPixels[i] = owners[i].Memory[..pixelCount];
        LayerPixels = layerPixels;
    }

    public ValueTask DisposeAsync()
    {
        Owner?.Dispose();
        foreach (var o in LayerOwners) o.Dispose();
        return ValueTask.CompletedTask;
    }

    public bool IsMultiLayer => LayerInfos.Count > 0;
}
