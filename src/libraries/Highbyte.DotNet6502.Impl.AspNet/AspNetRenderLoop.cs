using System.Diagnostics;
using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.Impl.AspNet;

public sealed class AspNetRenderLoop : IRenderLoop
{
    private readonly Action<double?>? _onBeforeRender;
    private readonly Action<double?>? _onAfterRender;
    private readonly Func<bool> _shouldEmitEmulationFrame;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _subscribed;

    public AspNetRenderLoop(
        Action<double?>? onBeforeRender = null,
        Action<double?>? onAfterRender = null,
        Func<bool>? shouldEmitEmulationFrame = null)
    {
        _onBeforeRender = onBeforeRender;
        _onAfterRender = onAfterRender;
        _shouldEmitEmulationFrame = shouldEmitEmulationFrame ?? (() => true);

        _subscribed = true;
    }

    public RenderTriggerMode Mode => RenderTriggerMode.HostFrameCallback;

    /// Fired once per Silk frame; argument is a monotonically increasing host time.
    public event EventHandler<TimeSpan>? FrameTick;

    /// For host-driven loops we don’t need to request redraws—Silk calls us per its cadence.
    /// Keep it as a harmless no-op to satisfy the interface.
    public void RequestRedraw() { /* no-op under Silk host-driven loop */ }

    /// <summary>
    /// Call this from the host UI thread when a frame has been painted (e.g., SKGLView.OnPaintSurface).
    /// This will raise FrameTick for any listeners (e.g., RenderCoordinator).
    /// </summary>
    public void RaiseFrameTick()
    {
        _onBeforeRender?.Invoke(null);

        // You can pass either accumulated clock time or the delta.
        // Most code prefers absolute time for animation curves:
        if (_shouldEmitEmulationFrame())
        {
            FrameTick?.Invoke(this, _clock.Elapsed);
        }

        _onAfterRender?.Invoke(null);
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            //_window.Render -= OnRender;
            _subscribed = false;
        }
    }
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
