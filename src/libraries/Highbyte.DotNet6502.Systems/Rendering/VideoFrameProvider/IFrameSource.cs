namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
public interface IFrameSource : IAsyncDisposable
{
    RenderSize NativeSize { get; }
    PixelFormat PixelFormat { get; }
    event EventHandler<RenderFrame>? FrameProduced;
    bool TryGetLatestFrame(out RenderFrame? frame);
}
