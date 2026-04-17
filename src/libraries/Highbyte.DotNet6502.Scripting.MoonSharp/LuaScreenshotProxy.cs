using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Captures the current emulator video frame and saves it as a PNG or JPEG image.
/// Used by the <c>emu.screenshot(filename [, quality])</c> Lua function.
/// Paths are sandboxed to the scripting base directory via <see cref="LuaFileProxy"/>.
/// </summary>
internal class LuaScreenshotProxy
{
    private readonly IHostApp _hostApp;
    private readonly LuaFileProxy _fileProxy;

    internal LuaScreenshotProxy(IHostApp hostApp, LuaFileProxy fileProxy)
    {
        _hostApp = hostApp;
        _fileProxy = fileProxy;
    }

    /// <summary>
    /// Composites the current video frame and saves it to <paramref name="filename"/>.
    /// Format is determined by file extension: <c>.png</c> (default), <c>.jpg</c>/<c>.jpeg</c>.
    /// </summary>
    /// <param name="filename">Path relative to the scripting base directory.</param>
    /// <param name="jpegQuality">JPEG quality 1–100, ignored for PNG. Default: 90.</param>
    internal void SaveScreenshot(string filename, int jpegQuality = 90)
    {
        var safePath = _fileProxy.GetSafePath(filename)
            ?? throw new ArgumentException($"emu.screenshot(): unsafe or invalid filename: {filename}");

        var layerProvider = _hostApp.CurrentSystemRunner?.System.RenderProvider as IVideoFrameLayerProvider
            ?? throw new InvalidOperationException(
                "emu.screenshot(): no IVideoFrameLayerProvider available. " +
                "Ensure a system is running and its render provider supports layer output.");

        var layers = layerProvider.Layers;
        if (layers.Count == 0)
            throw new InvalidOperationException("emu.screenshot(): render provider has no layers.");

        var size = layers[0].Size;
        int width = size.Width;
        int height = size.Height;

        // Snapshot layer pixel data as byte arrays (ReadOnlyMemory<uint> → byte[])
        var frontBuffers = layerProvider.CurrentFrontLayerBuffers;
        var byteSrcs = new List<ReadOnlyMemory<byte>>(frontBuffers.Count);
        foreach (var mem in frontBuffers)
        {
            // Zero-copy reinterpret of uint span as bytes, then copy to a local array
            // so the rasterizer can flip buffers freely after this point.
            var byteSpan = MemoryMarshal.AsBytes(mem.Span);
            byteSrcs.Add(byteSpan.ToArray());
        }

        // Composite all layers into a single BGRA32 flat buffer
        int dstStride = width * 4;
        var dst = new byte[height * dstStride];
        SoftwareLayerCompositor.FlattenRgba32(dst, dstStride, size, layers, byteSrcs);

        // Ensure output directory exists
        var dir = Path.GetDirectoryName(safePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // FlattenRgba32 output layout: [B, G, R, A] per pixel (BGRA32)
        using var image = Image.LoadPixelData<Bgra32>(dst, width, height);

        var ext = Path.GetExtension(filename).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg")
        {
            var encoder = new JpegEncoder { Quality = Math.Clamp(jpegQuality, 1, 100) };
            image.SaveAsJpeg(safePath, encoder);
        }
        else
        {
            image.SaveAsPng(safePath);
        }
    }
}
