using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.Impl.SadConsole;
/// An IRenderLoop that uses the MonoGame/SadConsole Draw cadence.
/// - continuous: raises FrameTick every Draw
/// - manualInvalidation: raises FrameTick only after RequestRedraw()
/// SadConsole v10 render loop: raises FrameTick on each SadConsole draw.
/// - continuous=true  -> every draw
/// - continuous=false -> only after RequestRedraw()
public sealed class SadConsoleRenderLoop : IRenderLoop
{
    private readonly Action<double>? _onBeforeRender;
    private readonly Action<double>? _onAfterRender;
    private readonly Func<bool> _shouldEmitEmulationFrame;

    private readonly bool _continuous;
    private bool _pendingRedraw;

    public SadConsoleRenderLoop(
        Action<double>? onBeforeRender = null,
        Action<double>? onAfterRender = null,
        Func<bool>? shouldEmitEmulationFrame = null,
        bool continuous = true)
    {
        _onBeforeRender = onBeforeRender;
        _onAfterRender = onAfterRender;
        _shouldEmitEmulationFrame = shouldEmitEmulationFrame ?? (() => true);
        _continuous = continuous;
        // Subscribe to the host's render event (fires once per drawn frame)
        Game.Instance.FrameRender += OnFrameRender;
    }

    public RenderTriggerMode Mode => _continuous ? RenderTriggerMode.HostFrameCallback
                                                 : RenderTriggerMode.ManualInvalidation;

    public event EventHandler<TimeSpan>? FrameTick;

    public void RequestRedraw()
    {
        if (!_continuous)
            _pendingRedraw = true;
    }

    private void OnFrameRender(object? sender, GameHost host)
    {
        // host.DrawFrameDelta is the elapsed time for this draw; total time available via host.GameRunningTotalTime
        var delta = host.DrawFrameDelta; // TimeSpan

        _onBeforeRender?.Invoke(delta.TotalMicroseconds);

        if (_shouldEmitEmulationFrame())
        {
            if (_continuous)
            {
                FrameTick?.Invoke(this, delta);
            }
            else if (_pendingRedraw)
            {
                _pendingRedraw = false;
                FrameTick?.Invoke(this, delta);
            }
        }

        _onAfterRender?.Invoke(delta.TotalMicroseconds);
    }

    public void Dispose()
    {
        Game.Instance.FrameRender -= OnFrameRender;
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
