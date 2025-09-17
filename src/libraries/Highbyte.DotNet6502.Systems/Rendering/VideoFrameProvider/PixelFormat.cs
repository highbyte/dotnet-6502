namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;

public enum PixelFormat
{
    Rgba32,   // 4 bytes per pixel
    Bgra32,
    Indexed8, // 1 byte per pixel, palette supplied elsewhere
    //Rgb565,   // 2 bytes per pixel
    // ... extend as you support more systems
}
