using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Highbyte.DotNet6502.Impl.Avalonia.Render;
using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Controls;

/// <summary>
/// A custom control that renders the emulator display using Avalonia WriteableBitmap for broad platform compatibility.
/// This control works efficiently on all Avalonia targets including WebAssembly.
/// </summary>
public class EmulatorBitmapDisplayControl : EmulatorDisplayControlBase
{
    private readonly IRenderCoordinator? _renderCoordinator;
    private readonly IAvaloniaBitmapRenderTarget? _avaloniaBitmapRenderTarget;

    static EmulatorBitmapDisplayControl()
    {
        AffectsRender<EmulatorBitmapDisplayControl>(ScaleProperty);
    }

    public EmulatorBitmapDisplayControl(
        IRenderCoordinator? renderCoordinator,
        IAvaloniaBitmapRenderTarget? avaloniaBitmapRenderTarget,
        double scale,
        bool focusable,
        Func<bool> shouldEmitEmulationFrame
        ) : base(shouldEmitEmulationFrame)
    {
        _renderCoordinator = renderCoordinator;
        _avaloniaBitmapRenderTarget = avaloniaBitmapRenderTarget;
        Scale = scale;
        Focusable = focusable;
    }

    protected override void OnRender(DrawingContext context)
    {
        if (_renderCoordinator == null) return;

        try
        {
            var valueTask = _renderCoordinator.FlushIfDirtyAsync();
            if (!valueTask.IsCompletedSuccessfully)
            {
                ObserveRenderTask(valueTask.AsTask());
            }
        }
        catch (Exception ex)
        {
            // Catch rendering exceptions and log only
            // Following official Avalonia best practice from ServerCompositionCustomVisual:
            // Only log exceptions in render methods, never re-throw or re-post
            System.Diagnostics.Debug.WriteLine($"OnRender exception: {ex}");

            // Logger.TryGet(LogEventLevel.Error, LogArea.Visual)
            //     ?.Log(_handler, $"Exception in {_handler.GetType().Name}.{nameof(CompositionCustomVisualHandler.OnRender)} {0}", e);

        }

        if (_avaloniaBitmapRenderTarget == null) return;
        var destRect = new Rect(0, 0, DisplayWidth * Scale, DisplayHeight * Scale);
        context.DrawImage(_avaloniaBitmapRenderTarget.Bitmap, destRect);
    }

    private static void ObserveRenderTask(Task task)
    {
        if (task.IsCompleted)
        {
            LogRenderFailure(task);
            return;
        }

        _ = task.ContinueWith(
            static completedTask => LogRenderFailure(completedTask),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void LogRenderFailure(Task task)
    {
        if (task.Exception is { } exception)
        {
            System.Diagnostics.Debug.WriteLine($"OnRender exception: {exception.GetBaseException()}");
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        //if (change.Property == RendererProperty)
        //{
        //    InvalidateVisual();
        //    InvalidateMeasure();
        //}
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
    }
}
