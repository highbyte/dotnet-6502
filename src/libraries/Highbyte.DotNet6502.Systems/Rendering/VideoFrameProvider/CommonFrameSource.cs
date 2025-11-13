using System.Buffers;

namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;

public sealed class CommonFrameSource : IFrameSource
{
    private readonly IVideoFrameProvider _videoFrameProvider;
    private readonly MemoryPool<uint> _poolUint = MemoryPool<uint>.Shared;
    private RenderFrame? _latest;

    public CommonFrameSource(IVideoFrameProvider videoFrameProvider)
    {
        _videoFrameProvider = videoFrameProvider;
        _videoFrameProvider.FrameCompleted += OnFrameCompleted;
    }

    public RenderSize NativeSize => _videoFrameProvider.NativeSize;
    public PixelFormat PixelFormat => _videoFrameProvider.PixelFormat;
    public event EventHandler<RenderFrame>? FrameProduced;

    private void OnFrameCompleted(object? s, EventArgs e)
    {
        RenderFrame frame;

        if (_videoFrameProvider is IVideoFrameLayerProvider layered)
        {
            var infos = layered.Layers;
            // Zero-copy: pass buffers directly from the rasterizer
            // The rasterizer's ReaderWriterLockSlim ensures thread-safe access
            var layerBuffers = layered.CurrentFrontLayerBuffers;

            // Create memory owners that don't actually own anything (zero-copy wrappers)
            var owners = new IMemoryOwner<uint>[infos.Count];
            for (var i = 0; i < infos.Count; i++)
            {
                owners[i] = new NonOwningMemoryOwner<uint>(layerBuffers[i]);
            }
            frame = new RenderFrame(infos, owners);
        }
        else
        {
            // TODO: Shouldn't this also be changed to zero-copy like above?
            // CurrentFrontBuffer is a ReadOnlyMemory<uint> (one pixel per uint).
            // Rent a uint buffer and copy the pixels into it. Use the multi-layer
            // RenderFrame ctor with a single layer so we can keep uint[] pixel ownership.
            var frontBuffer = _videoFrameProvider.CurrentFrontBuffer;
            // Zero-copy: wrap the ReadOnlyMemory<uint> in a non-owning memory owner
            var ownerUint = new NonOwningMemoryOwner<uint>(frontBuffer);

            var info = new LayerInfo(_videoFrameProvider.NativeSize, _videoFrameProvider.PixelFormat, _videoFrameProvider.StrideBytes, 1f, BlendMode.Normal, 0);
            var owners = new IMemoryOwner<uint>[] { ownerUint };
            frame = new RenderFrame(new[] { info }, owners);
        }

        var old = Interlocked.Exchange(ref _latest, frame);
        if (old is not null) _ = old.DisposeAsync();

        FrameProduced?.Invoke(this, frame);
    }

    public bool TryGetLatestFrame(out RenderFrame? frame)
    {
        frame = Interlocked.Exchange(ref _latest, null);
        return frame is not null;
    }

    public async ValueTask DisposeAsync()
    {
        _videoFrameProvider.FrameCompleted -= OnFrameCompleted;
        if (_latest is not null) await _latest.DisposeAsync();
    }
}
