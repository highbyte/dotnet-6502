using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger _logger;

    public event EventHandler<bool>? CloseRequested
    {
        add
        {
            // Note: Special code to handle creating ErrorUserControl directly (used in Browser and Desktop)
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

    public ErrorUserControl(ErrorViewModel viewModel, ILoggerFactory loggerFactory)
    {
        DataContext = viewModel;
        _logger = loggerFactory.CreateLogger(typeof(ErrorUserControl).Name);

        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    //private async void ContinueButton_Click(object? sender, RoutedEventArgs e)
    //{
    //    _logger.LogInformation("Continue button clicked.");
    //}

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Note: Special code to handle creating ErrorUserControl directly (used in Browser and Desktop)
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
