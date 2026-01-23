using System.Diagnostics;
using Highbyte.DotNet6502.Systems.Rendering;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SilkNet;
/// Wraps Silk.NET IWindow.Render (deltaSeconds) and raises FrameTick each frame.
public sealed class SilkOnRenderLoop : IRenderLoop
{
    private readonly IWindow _window;
    private readonly Action<double>? _onBeforeRender;
    private readonly Action<double>? _onAfterRender;
    private readonly Func<bool> _shouldEmitEmulationFrame;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ILogger _logger;
    private bool _subscribed;

    public SilkOnRenderLoop(
        IWindow window,
        ILogger logger,
        Action<double>? onBeforeRender = null,
        Action<double>? onAfterRender = null,
        Func<bool>? shouldEmitEmulationFrame = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _window.Render += OnRender;
        _subscribed = true;
        _onBeforeRender = onBeforeRender;
        _onAfterRender = onAfterRender;
        _shouldEmitEmulationFrame = shouldEmitEmulationFrame ?? (() => true);
    }

    public RenderTriggerMode Mode => RenderTriggerMode.HostFrameCallback;

    /// Fired once per Silk frame; argument is a monotonically increasing host time.
    public event EventHandler<TimeSpan>? FrameTick;

    /// For host-driven loops we don’t need to request redraws—Silk calls us per its cadence.
    /// Keep it as a harmless no-op to satisfy the interface.
    public void RequestRedraw() { /* no-op under Silk host-driven loop */ }

    private void OnRender(double deltaTime)
    {
        try
        {
            _onBeforeRender?.Invoke(deltaTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in OnBeforeRender");

            // For critical exceptions, we might want to rethrow to terminate the application
            if (ex is OutOfMemoryException || ex is StackOverflowException)
                throw;
        }

        try
        {
            // You can pass either accumulated clock time or the delta.
            // Most code prefers absolute time for animation curves:
            if (_shouldEmitEmulationFrame())
            {
                FrameTick?.Invoke(this, _clock.Elapsed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in FrameTick event");

            if (ex is OutOfMemoryException || ex is StackOverflowException)
                throw;
        }

        try
        {
            _onAfterRender?.Invoke(deltaTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in OnAfterRender");

            if (ex is OutOfMemoryException || ex is StackOverflowException)
                throw;
        }
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            _window.Render -= OnRender;
            _subscribed = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
