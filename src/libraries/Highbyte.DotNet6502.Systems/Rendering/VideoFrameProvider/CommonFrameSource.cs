namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;

public sealed class CommonFrameSource : IFrameSource
{
    private readonly IVideoFrameProvider _videoFrameProvider;
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
            // Zero-copy: pass ReadOnlyMemory<uint> buffers directly from the rasterizer
            var layerBuffers = layered.CurrentFrontLayerBuffers;

            frame = new RenderFrame(infos, layerBuffers);
        }
        else
        {
            // CurrentFrontBuffer is a ReadOnlyMemory<uint> (one pixel per uint).
            var frontBuffer = _videoFrameProvider.CurrentFrontBuffer;

            var info = new LayerInfo(_videoFrameProvider.NativeSize, _videoFrameProvider.PixelFormat, _videoFrameProvider.StrideBytes, 1f, BlendMode.Normal, 0);
            frame = new RenderFrame(new[] { info }, new[] { frontBuffer });
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
