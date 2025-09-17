using System.Buffers;

namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
/// <summary>
/// One complete frame worth of pixels. Owns the backing memory (via IMemoryOwner),
/// but exposes a Memory<byte> for zero-copy handoff to renderers.
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

    // Multi-layer path (advanced)
    public IReadOnlyList<LayerInfo> LayerInfos { get; }
    public IReadOnlyList<IMemoryOwner<byte>> LayerOwners { get; }
    public IReadOnlyList<Memory<byte>> LayerPixels { get; }

    // Single-buffer ctor
    public RenderFrame(RenderSize size, PixelFormat fmt, IMemoryOwner<byte> owner, TimeSpan timestamp = default)
    {
        Size = size; PixelFormat = fmt; Timestamp = timestamp;
        StrideBytes = size.StrideBytes(fmt);
        Owner = owner;
        Pixels = owner.Memory[..(StrideBytes * size.Height)];
        LayerInfos = Array.Empty<LayerInfo>();
        LayerOwners = Array.Empty<IMemoryOwner<byte>>();
        LayerPixels = Array.Empty<Memory<byte>>();
    }

    // Multi-layer ctor
    public RenderFrame(IReadOnlyList<LayerInfo> infos, IReadOnlyList<IMemoryOwner<byte>> owners, TimeSpan timestamp = default)
    {
        if (infos.Count != owners.Count) throw new ArgumentException("infos/owners length mismatch");
        LayerInfos = infos; LayerOwners = owners; Timestamp = timestamp;

        // Derive overall frame “Size/Format” from the top-most (or first) layer by convention
        Size = infos.Count > 0 ? infos[0].Size : default;
        PixelFormat = infos.Count > 0 ? infos[0].PixelFormat : default;
        StrideBytes = infos.Count > 0 ? infos[0].StrideBytes : 0;

        Owner = null; Pixels = Memory<byte>.Empty;
        var layerPixels = new Memory<byte>[owners.Count];
        for (var i = 0; i < owners.Count; i++)
            layerPixels[i] = owners[i].Memory[..(infos[i].StrideBytes * infos[i].Size.Height)];
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
