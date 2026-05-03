using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Highbyte.DotNet6502.Impl.Avalonia.Render;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Controls;

/// <summary>
/// A custom control that renders the emulator display using Avalonia commands.
/// This control coordinates with the AvaloniaCommandTarget to render using Avalonia drawing primitives.
/// </summary>
public class EmulatorAvaloniaCommandControl : EmulatorDisplayControlBase
{
    private readonly CommandCoordinator? _renderCoordinator;
    private readonly AvaloniaCommandTarget? _avaloniaCommandTarget;

    static EmulatorAvaloniaCommandControl()
    {
        AffectsRender<EmulatorAvaloniaCommandControl>(ScaleProperty);
    }

    public EmulatorAvaloniaCommandControl(
        CommandCoordinator? renderCoordinator,
        AvaloniaCommandTarget? avaloniaCommandTarget,
        double scale,
        bool focusable,
        Func<bool> shouldEmitEmulationFrame
        ) : base(shouldEmitEmulationFrame)
    {
        _renderCoordinator = renderCoordinator;
        _avaloniaCommandTarget = avaloniaCommandTarget;
        Scale = scale;
        Focusable = focusable;
    }

    protected override void OnRender(DrawingContext context)
    {
        if (_renderCoordinator == null || _avaloniaCommandTarget == null)
            return;

        try
        {
            // Apply scale transformation to match the expected display size.
            // CommandCoordinator.FlushIfDirtyAsync completes synchronously for this control,
            // so the drawing context remains valid for the full command execution.
            using (context.PushTransform(Matrix.CreateScale(Scale, Scale)))
            {
                _avaloniaCommandTarget.SetDrawingContext(context);

                var flushTask = _renderCoordinator.FlushIfDirtyAsync();
                if (!flushTask.IsCompletedSuccessfully)
                {
                    ObserveUnexpectedRenderTask(flushTask.AsTask());
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnRender exception: {ex}");
        }
        finally
        {
            _avaloniaCommandTarget.SetDrawingContext(null);
        }
    }

    private static void ObserveUnexpectedRenderTask(Task task)
    {
        if (task.IsCompleted)
        {
            LogUnexpectedRenderFailure(task);
            return;
        }

        _ = task.ContinueWith(
            static completedTask => LogUnexpectedRenderFailure(completedTask),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void LogUnexpectedRenderFailure(Task task)
    {
        if (task.Exception is { } exception)
        {
            System.Diagnostics.Debug.WriteLine($"OnRender exception: {exception.GetBaseException()}");
            return;
        }

        System.Diagnostics.Debug.WriteLine("OnRender flush unexpectedly continued asynchronously.");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
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
