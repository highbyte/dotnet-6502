using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Render;

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

        // Get source data spans
        var bgBytes = frame.LayerPixels[0].Span;
        var fgBytes = frame.LayerPixels[1].Span;

        // Calculate dimensions and validate
        var pixelCount = Math.Min(bgInfo.Size.Width * bgInfo.Size.Height, bgBytes.Length / 4);
        var bytesPerRow = TargetSize.Width * 4;

        // Check if we can do a fast copy without row padding
        if (frameBuffer.RowBytes == bytesPerRow)
        {
            // Fast path: direct compositing to framebuffer without intermediate allocation
            CompositeLayers(frameBuffer.Address, bgBytes, fgBytes, pixelCount);
        }
        else
        {
            // Slow path: row-by-row compositing due to row padding
            CompositeLayersWithPadding(frameBuffer.Address, frameBuffer.RowBytes, bgBytes, fgBytes, TargetSize.Width, TargetSize.Height);
        }

        return ValueTask.CompletedTask;
    }

    private void CompositeLayers(IntPtr destination, ReadOnlySpan<byte> bgBytes, ReadOnlySpan<byte> fgBytes, int pixelCount)
    {
        // Use MemoryMarshal to cast spans to uint spans for more efficient processing
        var bgPixels = MemoryMarshal.Cast<byte, uint>(bgBytes);
        var fgPixels = MemoryMarshal.Cast<byte, uint>(fgBytes);

        // Allocate a temporary buffer for the composited result
        Span<uint> resultPixels = stackalloc uint[Math.Min(pixelCount, 1024)]; // Use stack allocation for small chunks
        var remainingPixels = pixelCount;
        var processedPixels = 0;

        while (remainingPixels > 0)
        {
            var chunkSize = Math.Min(remainingPixels, resultPixels.Length);
            var bgChunk = bgPixels.Slice(processedPixels, chunkSize);
            var fgChunk = fgPixels.Slice(processedPixels, chunkSize);
            var resultChunk = resultPixels[..chunkSize];

            // Single loop compositing - copy background and blend foreground in one pass
            for (int i = 0; i < chunkSize; i++)
            {
                var fgPixel = fgChunk[i];

                // Use direct uint comparison for transparency check
                if (fgPixel != TRANSPARENT_COLOR)
                {
                    resultChunk[i] = fgPixel; // Foreground pixel (opaque, overwrite background)
                }
                else
                {
                    resultChunk[i] = bgChunk[i]; // Background pixel (foreground is transparent)
                }
            }

            // Copy the composited chunk to destination
            var resultBytes = MemoryMarshal.AsBytes(resultChunk);
            var destinationOffset = IntPtr.Add(destination, processedPixels * 4);
            Marshal.Copy(resultBytes.ToArray(), 0, destinationOffset, resultBytes.Length);

            processedPixels += chunkSize;
            remainingPixels -= chunkSize;
        }
    }

    private void CompositeLayersWithPadding(IntPtr destination, int destRowBytes, ReadOnlySpan<byte> bgBytes, ReadOnlySpan<byte> fgBytes, int width, int height)
    {
        var bgPixels = MemoryMarshal.Cast<byte, uint>(bgBytes);
        var fgPixels = MemoryMarshal.Cast<byte, uint>(fgBytes);
        var srcRowPixels = width;

        // Allocate row buffer on stack for better performance
        Span<uint> rowBuffer = stackalloc uint[width];

        for (int y = 0; y < height; y++)
        {
            var srcRowOffset = y * srcRowPixels;
            var bgRow = bgPixels.Slice(srcRowOffset, width);
            var fgRow = fgPixels.Slice(srcRowOffset, width);

            // Composite this row
            for (int x = 0; x < width; x++)
            {
                var fgPixel = fgRow[x];

                if (fgPixel != TRANSPARENT_COLOR)
                {
                    rowBuffer[x] = fgPixel;
                }
                else
                {
                    rowBuffer[x] = bgRow[x];
                }
            }

            // Copy row to destination
            var rowBytes = MemoryMarshal.AsBytes(rowBuffer);
            var destRowPtr = IntPtr.Add(destination, y * destRowBytes);
            Marshal.Copy(rowBytes.ToArray(), 0, destRowPtr, width * 4);
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
