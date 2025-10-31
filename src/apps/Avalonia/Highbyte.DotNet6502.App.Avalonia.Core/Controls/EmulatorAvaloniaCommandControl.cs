using System;
using Avalonia;
using Avalonia.Media;
using Highbyte.DotNet6502.App.Avalonia.Core.Render;
using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Controls;

/// <summary>
/// A custom control that renders the emulator display using Avalonia commands.
/// This control coordinates with the AvaloniaCommandTarget to render using Avalonia drawing primitives.
/// </summary>
public class EmulatorAvaloniaCommandControl : EmulatorDisplayControlBase
{
    private readonly IRenderCoordinator? _renderCoordinator;
    private readonly AvaloniaCommandTarget? _avaloniaCommandTarget;

    static EmulatorAvaloniaCommandControl()
    {
        AffectsRender<EmulatorAvaloniaCommandControl>(ScaleProperty);
    }

    public EmulatorAvaloniaCommandControl(
        IRenderCoordinator? renderCoordinator,
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

    protected override async void OnRender(DrawingContext context)
    {
        if (_renderCoordinator == null || _avaloniaCommandTarget == null)
            return;

        // Apply scale transformation to match the expected display size
        using (context.PushTransform(Matrix.CreateScale(Scale, Scale)))
        {
            // Set the drawing context on the command target so it can render
            _avaloniaCommandTarget.SetDrawingContext(context);

            // FlushIfDirtyAsync will invoke the render target, which will execute commands using the Avalonia drawing context
            await _renderCoordinator.FlushIfDirtyAsync();

            // Clear the drawing context after rendering
            _avaloniaCommandTarget.SetDrawingContext(null);
        }
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
