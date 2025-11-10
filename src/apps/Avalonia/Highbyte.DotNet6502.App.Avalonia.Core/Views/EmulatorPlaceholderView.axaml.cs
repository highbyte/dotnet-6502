using System;
using Avalonia;
using Avalonia.Controls;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

/// <summary>
/// View for displaying C64 emulator using WriteableBitmap-based rendering.
/// This view is optimized for WebAssembly and cross-platform compatibility.
/// </summary>
public partial class EmulatorPlaceholderView : UserControl
{
    private EmulatorPlaceholderViewModel? _subscribedViewModel;

    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<EmulatorPlaceholderView, double>(nameof(Scale), 2.0);

    private int _displayWidth = 320;
    private int _displayHeight = 200;

    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public EmulatorPlaceholderView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous ViewModel's events and property changes
        if (_subscribedViewModel != null)
        {
            //_subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel.SizeChanged -= OnSizeChanged;
        }

        // Subscribe to new ViewModel's events and property changes
        _subscribedViewModel = DataContext as EmulatorPlaceholderViewModel;
        if (_subscribedViewModel != null)
        {
            //_subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _subscribedViewModel.SizeChanged += OnSizeChanged;
        }
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        SetDisplaySize(
            _subscribedViewModel?.ScreenInfo?.VisibleWidth ?? 320,
            _subscribedViewModel?.ScreenInfo?.VisibleHeight ?? 200);
    }

    private void SetDisplaySize(int width, int height)
    {
        _displayWidth = width;
        _displayHeight = height;

        InvalidateMeasure();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ScaleProperty)
        {
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var scaledWidth = _displayWidth * Scale;
        var scaledHeight = _displayHeight * Scale;
        return new Size(scaledWidth, scaledHeight);
    }
}
