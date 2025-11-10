using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Controls;

/// <summary>
/// A custom control that renders the emulator display using WriteableBitmap for broad platform compatibility.
/// This control works efficiently on all Avalonia targets including WebAssembly.
/// </summary>
public abstract class EmulatorDisplayControlBase : Control
{
    private readonly Func<bool> _shouldEmitEmulationFrame;

    private int _displayWidth = 320;
    private int _displayHeight = 200;

    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<EmulatorBitmapDisplayControl, double>(nameof(Scale), 2.0);

    //public static readonly StyledProperty<Action?> UpdateRenderFPSCallbackProperty =
    //    AvaloniaProperty.Register<EmulatorBitmapDisplayControl, Action?>(nameof(UpdateRenderFPSCallback));

    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    //public Action? UpdateRenderFPSCallback
    //{
    //    get => GetValue(UpdateRenderFPSCallbackProperty);
    //    set => SetValue(UpdateRenderFPSCallbackProperty, value);
    //}

    /// <summary>
    /// Gets the current display width
    /// </summary>
    public int DisplayWidth => _displayWidth;

    /// <summary>
    /// Gets the current display height
    /// </summary>
    public int DisplayHeight => _displayHeight;

    /// <summary>
    /// Set the display dimensions based on the emulated system screen
    /// </summary>
    /// <param name="width">Display width in pixels</param>
    /// <param name="height">Display height in pixels</param>
    public void SetDisplaySize(int width, int height)
    {
        _displayWidth = width;
        _displayHeight = height;
        InvalidateVisual();
        InvalidateMeasure();
    }

    /// <summary>
    /// Call this method to refresh the display when a new frame is available
    /// </summary>
    public void RefreshDisplay()
    {
        InvalidateVisual();
    }

    static EmulatorDisplayControlBase()
    {
    }

    public EmulatorDisplayControlBase(Func<bool> shouldEmitEmulationFrame)
    {
        _shouldEmitEmulationFrame = shouldEmitEmulationFrame;
    }

    public override async void Render(DrawingContext context)
    {
        if (_shouldEmitEmulationFrame())
            OnRender(context);
    }

    protected abstract void OnRender(DrawingContext context);

    protected override Size MeasureOverride(Size availableSize)
    {
        var scaledWidth = _displayWidth * Scale;
        var scaledHeight = _displayHeight * Scale;
        return new Size(scaledWidth, scaledHeight);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ScaleProperty)
        {
            InvalidateVisual();
            InvalidateMeasure();
        }
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
