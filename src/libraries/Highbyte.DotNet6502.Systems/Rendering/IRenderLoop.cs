namespace Highbyte.DotNet6502.Systems.Rendering;

public interface IRenderLoop : IDisposable, IAsyncDisposable
{
    public RenderTriggerMode Mode { get; }

    /// For HostFrameCallback: raised each frame-tick from the host app's render loop (UI thread).
    public event EventHandler<TimeSpan>? FrameTick;

    /// For ManualInvalidation: call to ask the host to schedule a render soon.
    public void RequestRedraw();
}

public enum RenderTriggerMode
{
    HostFrameCallback, // e.g., Avalonia’s CompositionTarget.Rendering / OnRender / game loop
    ManualInvalidation // you must call InvalidateVisual()/RequestAnimationFrame() yourself
}
