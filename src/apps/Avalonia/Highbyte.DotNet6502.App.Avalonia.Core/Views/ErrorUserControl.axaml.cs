using System;
using Avalonia.Controls;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

/// <summary>
/// UserControl for displaying error messages with Continue/Exit options.
/// Includes an optional expandable section to show detailed exception information and stack trace.
/// </summary>
public partial class ErrorUserControl : UserControl
{
    private ErrorViewModel? ViewModel => DataContext as ErrorViewModel;
    private ErrorViewModel? _previousViewModel;

    private EventHandler<bool>? _closeRequestedHandlers;

    public event EventHandler<bool>? CloseRequested
    {
        add
        {
            // Note: Special code to handle creating ErrorUserControl directly (used in Browser) or via ErrorUserDialog Window (used in Desktop app)
            _closeRequestedHandlers += value;
            // If ViewModel is already available, subscribe immediately
            ViewModel?.CloseRequested += value;
        }
        remove
        {
            _closeRequestedHandlers -= value;
            // Unsubscribe from ViewModel if available
            ViewModel?.CloseRequested -= value;
        }
    }

    public ErrorUserControl(ErrorViewModel viewModel)
    {
        DataContext = viewModel;

        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Note: Special code to handle creating ErrorUserControl directly (used in Browser) or via ErrorDialog Window (used in Desktop app)
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
}
