using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.Impl.Terminal;

/// <summary>
/// A manual-invalidation <see cref="IRenderLoop"/> for the terminal host.
///
/// The emulator's command stream raises <c>FrameCompleted</c> each emulated frame; the render
/// coordinator turns that into a <see cref="RequestRedraw"/> call. The host's display timer then
/// polls <see cref="ConsumeRedrawRequested"/> at a throttled display rate and, when a new frame is
/// pending, flushes the command stream into the render target and repaints the terminal view.
///
/// This decouples the terminal's (slower) display refresh from the emulator's internal frame rate,
/// as recommended for terminal rendering.
/// </summary>
public sealed class TerminalRenderLoop : IRenderLoop
{
    public RenderTriggerMode Mode => RenderTriggerMode.ManualInvalidation;

    // Not used in manual-invalidation mode (the coordinator subscribes to the command stream's
    // FrameCompleted instead), but required by the interface.
#pragma warning disable CS0067 // Event is never used (required by IRenderLoop)
    public event EventHandler<TimeSpan>? FrameTick;
#pragma warning restore CS0067

    private volatile bool _redrawRequested;

    public void RequestRedraw() => _redrawRequested = true;

    /// <summary>
    /// Returns whether a redraw has been requested since the last call, clearing the flag.
    /// Polled by the host's throttled display timer.
    /// </summary>
    public bool ConsumeRedrawRequested()
    {
        if (!_redrawRequested)
            return false;
        _redrawRequested = false;
        return true;
    }

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
