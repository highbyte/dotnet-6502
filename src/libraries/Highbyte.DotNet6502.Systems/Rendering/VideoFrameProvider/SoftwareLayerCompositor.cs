namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
/// <summary>
/// Helper class for CPU-based compositing of multiple layers into a single RGBA32 buffer.
/// Useful if a render target does not support GPU compositing with shaders.
/// </summary>
public static class SoftwareLayerCompositor
{
    public static void FlattenRgba32(
        Span<byte> dst, int dstStride, RenderSize size,
        IReadOnlyList<LayerInfo> layers, IReadOnlyList<ReadOnlyMemory<byte>> srcs)
    {
        // Clear
        for (var y = 0; y < size.Height; y++)
            dst.Slice(y * dstStride, size.Width * 4).Clear();

        // Sort by Z
        var order = Enumerable.Range(0, layers.Count)
                              .OrderBy(i => layers[i].ZOrder)
                              .ToArray();

        foreach (var i in order)
        {
            var li = layers[i];
            if (li.PixelFormat is not (PixelFormat.Rgba32 or PixelFormat.Bgra32))
                throw new NotSupportedException("CPU flatten only supports RGBA/BGRA here.");

            var src = srcs[i].Span;
            var op = Math.Clamp(li.Opacity, 0f, 1f);
            var bgra = li.PixelFormat == PixelFormat.Bgra32;

            for (var y = 0; y < li.Size.Height; y++)
            {
                var sRow = src.Slice(y * li.StrideBytes, li.Size.Width * 4);
                var dRow = dst.Slice(y * dstStride, li.Size.Width * 4);

                for (var x = 0; x < li.Size.Width; x++)
                {
                    var si = x * 4;
                    byte b = sRow[si + 0], g = sRow[si + 1], r = sRow[si + 2], a = sRow[si + 3];
                    if (!bgra) (r, b) = (b, r); // swap if RGBA

                    var af = a / 255f * op;
                    if (af <= 0f) continue;

                    var di = si;
                    float dr = dRow[di + 2], dg = dRow[di + 1], db = dRow[di + 0], da = dRow[di + 3];
                    // Normal alpha over
                    var inv = 1f - af;
                    dr = r * af + dr * inv;
                    dg = g * af + dg * inv;
                    db = b * af + db * inv;
                    da = 255f * (af + da / 255f * inv);

                    dRow[di + 2] = (byte)dr; dRow[di + 1] = (byte)dg; dRow[di + 0] = (byte)db; dRow[di + 3] = (byte)da;
                }
            }
        }
    }
}

