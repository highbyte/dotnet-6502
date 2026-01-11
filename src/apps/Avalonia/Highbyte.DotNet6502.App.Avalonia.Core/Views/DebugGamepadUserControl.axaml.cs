using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class DebugGamepadUserControl : UserControl
{
    private DebugGamepadViewModel? _previousViewModel;

    private DebugGamepadViewModel? ViewModel => DataContext as DebugGamepadViewModel;

    private EventHandler<bool>? _closeRequestedHandlers;

    public event EventHandler<bool>? CloseRequested
    {
        add
        {
            _closeRequestedHandlers += value;
            if (ViewModel != null)
            {
                ViewModel.CloseRequested += value;
            }
        }
        remove
        {
            _closeRequestedHandlers -= value;
            if (ViewModel != null)
            {
                ViewModel.CloseRequested -= value;
            }
        }
    }

    public DebugGamepadUserControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous ViewModel
        if (_previousViewModel != null)
        {
            _previousViewModel.StopPolling();
            if (_closeRequestedHandlers != null)
                _previousViewModel.CloseRequested -= _closeRequestedHandlers;
        }

        // Subscribe to new ViewModel
        if (ViewModel != null)
        {
            if (_closeRequestedHandlers != null)
                ViewModel.CloseRequested += _closeRequestedHandlers;

            // Start polling when the view is displayed
            ViewModel.StartPolling();
        }

        _previousViewModel = ViewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
