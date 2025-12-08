using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class DebugSoundUserControl : UserControl
{
    private DebugSoundViewModel? _previousViewModel;

    private DebugSoundViewModel? ViewModel => DataContext as DebugSoundViewModel;

    private EventHandler<bool>? _closeRequestedHandlers;

    public event EventHandler<bool>? CloseRequested
    {
        add
        {
            // Note: Special code to handle creating DebugSoundUserControl directly (used in Browser) or via DebugSoundConfigDialog Window (may in future used in Desktop app)
            _closeRequestedHandlers += value;
            // If ViewModel is already available, subscribe immediately
            if (ViewModel != null)
            {
                ViewModel.CloseRequested += value;
            }
        }
        remove
        {
            _closeRequestedHandlers -= value;
            // Unsubscribe from ViewModel if available
            if (ViewModel != null)
            {
                ViewModel.CloseRequested -= value;
            }
        }
    }

    public DebugSoundUserControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Note: Special code to handle creating C64ConfigUserControl directly (used in Browser) or via C64ConfigDialog Window (used in Desktop app)
        // Unsubscribe from previous ViewModel
        if (_previousViewModel != null)
        {
            if (_closeRequestedHandlers != null)
                _previousViewModel.CloseRequested -= _closeRequestedHandlers;
        }

        // Subscribe to new ViewModel
        if (ViewModel != null)
        {
            if (_closeRequestedHandlers != null)
                ViewModel.CloseRequested += _closeRequestedHandlers;
        }

        _previousViewModel = ViewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void PlayWaveform_Click(object? sender, RoutedEventArgs e)
    {
        // TODO
    }
}
