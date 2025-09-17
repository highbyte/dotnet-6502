namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;

public interface IRenderFrameTarget : IRenderTarget, IAsyncDisposable
{
    public RenderSize TargetSize { get; }
    public PixelFormat AcceptedFormat { get; } // convert if needed elsewhere

    /// True if the target can take multiple layers and composite (GPU or CPU).
    public bool SupportsCompositing { get; }
    public ValueTask PresentAsync(RenderFrame frame, CancellationToken ct = default);
}
