using System.Buffers;

namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;

public sealed class CommonFrameSource : IFrameSource
{
    private readonly IVideoFrameProvider _videoFrameProvider;
    private readonly MemoryPool<byte> _pool = MemoryPool<byte>.Shared;
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
            var owners = new IMemoryOwner<byte>[infos.Count];
            for (var i = 0; i < infos.Count; i++)
            {
                var bytes = infos[i].StrideBytes * infos[i].Size.Height;
                var owner = _pool.Rent(bytes);
                layered.CurrentFrontLayerBuffers[i].Span.CopyTo(owner.Memory.Span);
                owners[i] = owner;
            }
            frame = new RenderFrame(infos, owners);
        }
        else
        {
            var bytes = _videoFrameProvider.StrideBytes * _videoFrameProvider.NativeSize.Height;
            var owner = _pool.Rent(bytes);
            _videoFrameProvider.CurrentFrontBuffer.Span.CopyTo(owner.Memory.Span);
            frame = new RenderFrame(_videoFrameProvider.NativeSize, _videoFrameProvider.PixelFormat, owner);
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
