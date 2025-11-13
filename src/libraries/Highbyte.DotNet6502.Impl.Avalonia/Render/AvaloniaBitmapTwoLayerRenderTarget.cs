using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Impl.Avalonia.Render;

[DisplayName("Avalonia 2-layer Bitmap")]
[HelpText("Renders two layers to a Avalonia Bitmap using CPU for compositing.\nIt uses two RenderFrames (byte arrays) provided by the render source.\nThe render source must provide exactly two layers: background and foreground.")]
public sealed class AvaloniaBitmapTwoLayerRenderTarget : IRenderFrameTarget, IAvaloniaBitmapRenderTarget
{
    private WriteableBitmap _writeableBitmap = null!;
    public WriteableBitmap Bitmap => _writeableBitmap;

    private const uint TRANSPARENT_COLOR = 0x00000000; // Transparent black

    public string Name => "AvaloniaBitmapTwoLayerRenderTarget";

    public RenderSize TargetSize { get; }
    public Systems.Rendering.VideoFrameProvider.PixelFormat AcceptedFormat { get; } = Systems.Rendering.VideoFrameProvider.PixelFormat.Rgba32; // TODO: What to I need AcceptedFormat for?

    public bool SupportsCompositing => true;


    public AvaloniaBitmapTwoLayerRenderTarget(RenderSize size)
    {
        TargetSize = size;
        InitWriteableBitmap(size);
    }

    // Present two layers via GPU shader compositing
    public ValueTask PresentAsync(RenderFrame frame, CancellationToken ct = default)
    {
        return PresentAsyncAvaloniaBitmap(frame, ct);
    }

    private ValueTask PresentAsyncAvaloniaBitmap(RenderFrame frame, CancellationToken ct = default)
    {
        if (!frame.IsMultiLayer || frame.LayerInfos.Count != 2 || frame.LayerPixels.Count != 2)
            throw new ArgumentException("SkiaCanvasTwoLayerRenderTarget expects exactly 2 layers (background, foreground).", nameof(frame));

        if (_writeableBitmap == null) return ValueTask.CompletedTask;

        var bgInfo = frame.LayerInfos[0];
        var fgInfo = frame.LayerInfos[1];

        // Copy pixel data to WriteableBitmap
        using var frameBuffer = _writeableBitmap.Lock();

        // Get source data spans (already uint[])
        var bgPixels = frame.LayerPixels[0].Span;
        var fgPixels = frame.LayerPixels[1].Span;

        // Calculate dimensions and validate
        var pixelCount = Math.Min(bgInfo.Size.Width * bgInfo.Size.Height, bgPixels.Length);
        var bytesPerRow = TargetSize.Width * 4;

        // Check if we can do a fast copy without row padding
        if (frameBuffer.RowBytes == bytesPerRow)
        {
            // Fast path: direct compositing to framebuffer without intermediate allocation
            CompositeLayers(frameBuffer.Address, bgPixels, fgPixels, pixelCount);
        }
        else
        {
            // Slow path: row-by-row compositing due to row padding
            CompositeLayersWithPadding(frameBuffer.Address, frameBuffer.RowBytes, bgPixels, fgPixels, TargetSize.Width, TargetSize.Height);
        }

        return ValueTask.CompletedTask;
    }

    private unsafe void CompositeLayers(IntPtr destination, ReadOnlySpan<uint> bgPixels, ReadOnlySpan<uint> fgPixels, int pixelCount)
    {
        // Direct pointer access - zero-copy, no allocations
        uint* destPtr = (uint*)destination;

        // Composite directly to destination buffer
        for (int i = 0; i < pixelCount; i++)
        {
            var fgPixel = fgPixels[i];

            // Use direct uint comparison for transparency check
            destPtr[i] = fgPixel != TRANSPARENT_COLOR 
                ? fgPixel           // Foreground pixel (opaque, overwrite background)
                : bgPixels[i];      // Background pixel (foreground is transparent)
        }
    }

    private unsafe void CompositeLayersWithPadding(IntPtr destination, int destRowBytes, ReadOnlySpan<uint> bgPixels, ReadOnlySpan<uint> fgPixels, int width, int height)
    {
        var srcRowPixels = width;
        byte* baseDestPtr = (byte*)destination;

        for (int y = 0; y < height; y++)
        {
            var srcRowOffset = y * srcRowPixels;
            var bgRow = bgPixels.Slice(srcRowOffset, width);
            var fgRow = fgPixels.Slice(srcRowOffset, width);

            // Get pointer to this row (accounting for row padding)
            uint* destRowPtr = (uint*)(baseDestPtr + (y * destRowBytes));

            // Composite directly to destination row
            for (int x = 0; x < width; x++)
            {
                var fgPixel = fgRow[x];
                destRowPtr[x] = fgPixel != TRANSPARENT_COLOR ? fgPixel : bgRow[x];
            }
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void InitWriteableBitmap(RenderSize size)
    {
        var width = size.Width;
        var height = size.Height;

        // Create WriteableBitmap with BGRA8888 format for optimal performance
        _writeableBitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            global::Avalonia.Platform.PixelFormat.Bgra8888,
            AlphaFormat.Premul);
    }
}
