using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Render;
public sealed class AvaloniaInvalidateRenderLoop : IRenderLoop
{
    private readonly Func<Control?> _getControl;
    private readonly Func<bool> _shouldEmitEmulationFrame;
    //private readonly Stopwatch _clock = Stopwatch.StartNew();

    public AvaloniaInvalidateRenderLoop(
        Func<Control?> getControl,
        Func<bool>? shouldEmitEmulationFrame = null)
    {
        _getControl = getControl;
        _shouldEmitEmulationFrame = shouldEmitEmulationFrame ?? (() => true);
    }

    public RenderTriggerMode Mode => RenderTriggerMode.ManualInvalidation;

    /// Fired once per Silk frame; argument is a monotonically increasing host time.
    public event EventHandler<TimeSpan>? FrameTick;

    /// For host-driven loops 
    public void RequestRedraw()
    {
        _getControl()?.InvalidateVisual();
    }

    public void Dispose()
    {
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
