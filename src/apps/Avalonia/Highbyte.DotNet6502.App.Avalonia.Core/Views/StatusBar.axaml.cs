using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class StatusBar : UserControl
{
    public StatusBar()
    {
        InitializeComponent();
    }

    private void SpeedFlyoutOpening(object? sender, EventArgs e)
    {
        // Keep the editor in the emulator window's visual and accessibility trees.
        if (sender is Flyout flyout)
            flyout.Popup.ShouldUseOverlayLayer = true;
        (DataContext as MainViewModel)?.RefreshSpeedEditor();
    }

    private void ApplySpeedClicked(object? sender, RoutedEventArgs e)
        => (DataContext as MainViewModel)?.ApplyCustomSpeed();

    private void SpeedTextKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            (DataContext as MainViewModel)?.ApplyCustomSpeed();
            e.Handled = true;
        }
    }
}
