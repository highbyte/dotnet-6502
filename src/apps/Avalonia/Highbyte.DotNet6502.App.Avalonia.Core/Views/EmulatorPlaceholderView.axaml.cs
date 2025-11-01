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

    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<EmulatorPlaceholderView, double>(nameof(Scale), 2.0);

    private int _displayWidth = 320;
    private int _displayHeight = 200;

    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    // Access HostApp through ViewModel
    private AvaloniaHostApp? HostApp
    {
        get
        {
            // Navigate up the visual tree to find MainView's DataContext
            var parent = this.Parent;
            while (parent != null)
            {
                if (parent is MainView mainView && mainView.DataContext is MainViewModel mainViewModel)
                {
                    return mainViewModel.HostApp;
                }
                if (parent is Control control)
                    parent = control.Parent;
                else
                    break;
            }
            return null;
        }
    }

    public EmulatorPlaceholderView()
    {
        InitializeComponent();
    }

    public void SetDisplaySize(int width, int height)
    {
        _displayWidth = width;
        _displayHeight = height;
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
