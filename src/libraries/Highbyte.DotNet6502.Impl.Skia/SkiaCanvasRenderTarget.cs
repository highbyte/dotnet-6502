using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Impl.Skia;

[DisplayName("Skia Canvas")]
[HelpText("Renders to a SkiaSharp SKCanvas.\nIt uses a RenderFrame (byte array) provided by the render source.")]
public sealed class SkiaCanvasTarget : IRenderFrameTarget
{
    private readonly Func<SKCanvas?> _canvasAccessor; // provided by host per-frame

    public string Name => "SkiaCanvasTarget";

    public RenderSize TargetSize { get; }
    public PixelFormat AcceptedFormat { get; } = PixelFormat.Rgba32; // TODO: What to I need AcceptedFormat for?

    public bool SupportsCompositing => false;

    public SkiaCanvasTarget(RenderSize size, Func<SKCanvas?> canvasAccessor)
    {
        TargetSize = size;
        _canvasAccessor = canvasAccessor;
    }

    //public ValueTask PresentAsync(RenderFrame frame, CancellationToken ct = default)
    //{
    //    var canvas = _canvasAccessor();
    //    if (canvas is null) return ValueTask.CompletedTask;

    //    var skColorType = frame.PixelFormat switch
    //    {
    //        PixelFormat.Rgba32 => SKColorType.Rgba8888,
    //        PixelFormat.Bgra32 => SKColorType.Bgra8888,
    //        _ => throw new NotSupportedException($"Pixel format {frame.PixelFormat} not supported in {nameof(SkiaCanvasTarget)}")
    //    };
    //    // Create image directly from managed bytes without unsafe pointers
    //    using var data = SKData.CreateCopy(frame.Pixels.Span);
    //    using var img = SKImage.FromPixels(
    //        new SKImageInfo(TargetSize.Width, TargetSize.Height, skColorType, SKAlphaType.Unpremul),
    //        data,
    //        frame.StrideBytes);

    //    canvas.DrawImage(img, 0, 0);

    //    // If the canvas is GPU-backed and you want to be extra cautious that the upload completes
    //    // before unpinning, you can optionally flush:
    //    // canvas.Flush();

    //    return ValueTask.CompletedTask;
    //}

    // zero-copy of your frame buffer, but still one upload when drawing to a GPU surface. Best when your RenderFrame memory is already in the right format/stride.
    public unsafe ValueTask PresentAsync(RenderFrame frame, CancellationToken ct = default)
    {
        var canvas = _canvasAccessor();
        if (canvas is null) return ValueTask.CompletedTask;

        var skColorType = frame.PixelFormat switch
        {
            PixelFormat.Rgba32 => SKColorType.Rgba8888,
            PixelFormat.Bgra32 => SKColorType.Bgra8888,
            _ => throw new NotSupportedException($"Pixel format {frame.PixelFormat} not supported in {nameof(SkiaCanvasTarget)}")
        };

        var info = new SKImageInfo(TargetSize.Width, TargetSize.Height, skColorType, SKAlphaType.Unpremul);

        // Pin managed memory for the duration of the draw
        using var mh = frame.Pixels.Pin();
        var ptr = (IntPtr)mh.Pointer;

        using var img = SKImage.FromPixels(info, ptr, frame.StrideBytes);
        canvas.DrawImage(img, 0, 0);

        // If the canvas is GPU-backed and you want to be extra cautious that the upload completes
        // before unpinning, you can optionally flush:
        //canvas.Flush();

        return ValueTask.CompletedTask;
    }

    // Adds one CPU copy into a persistent pinned buffer, but avoids per-frame SKImage/SKData allocations and favors DrawBitmap hot paths. Useful when you want to reuse resources and keep GC pressure minimal.
    //public unsafe ValueTask PresentAsync(RenderFrame frame, CancellationToken ct = default)
    //{

    //    // In ctor/init:
    //    //_gcHandle = GCHandle.Alloc(_backing, GCHandleType.Pinned);
    //    //_bitmap = new SKBitmap();
    //    //_bitmap.InstallPixels(
    //    //    new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul),
    //    //    _gcHandle.AddrOfPinnedObject(),
    //    //    w * 4,
    //    //    releaseProc: null, // free in Dispose
    //    //    context: null);


    //    var canvas = _canvasAccessor();
    //    if (canvas is null) return ValueTask.CompletedTask;

    //    frame.Pixels.Span.CopyTo(MemoryMarshal.AsBytes(frame.Pixels.Span));
    //    canvas.DrawBitmap(_bitmap, 0, 0);

    //    // If the canvas is GPU-backed and you want to be extra cautious that the upload completes
    //    // before unpinning, you can optionally flush:
    //    //canvas.Flush();

    //    return ValueTask.CompletedTask;
    //}


    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

}
