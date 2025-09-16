namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;

public interface IVideoFrameProvider : IRenderSource
{
    public RenderSize NativeSize { get; }        // e.g., 384x272 including borders or 320x200 visible
    public PixelFormat PixelFormat { get; }      // what the provider writes (e.g., Bgra32)
    // Called by VIC-II when a line finishes, or when a frame finishes.
    public event EventHandler<int>? ScanlineCompleted;
    public event EventHandler? FrameCompleted;

    // Non-owning view to the *current* frame buffer (double-buffered internally)
    public ReadOnlyMemory<byte> CurrentFrontBuffer { get; }
    public int StrideBytes { get; }

    // Called by emulator to swap buffers at end-of-frame
    public void FlipBuffers();
}
