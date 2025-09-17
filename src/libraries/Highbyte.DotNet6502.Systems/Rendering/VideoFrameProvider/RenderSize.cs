namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;

public readonly record struct RenderSize(int Width, int Height)
{
    public int StrideBytes(PixelFormat fmt) => fmt switch
    {
        PixelFormat.Rgba32 => Width * 4,
        PixelFormat.Bgra32 => Width * 4,
        PixelFormat.Indexed8 => Width,
        _ => throw new NotSupportedException(),
    };
}
