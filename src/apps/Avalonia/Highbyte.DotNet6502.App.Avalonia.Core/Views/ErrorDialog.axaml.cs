using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

/// <summary>
/// Dialog window for displaying error messages with Continue/Exit options.
/// Includes an optional expandable section to show detailed exception information and stack trace.
/// </summary>
public partial class ErrorDialog : Window
{
    public enum ErrorDialogResult
    {
        Continue,
        Exit
    }

    public ErrorDialogResult UserChoice { get; private set; } = ErrorDialogResult.Exit;
    public string ErrorMessage { get; }
    public string? ExceptionDetails { get; }
    public bool HasException { get; }

    private bool _showDetails = false;
    public bool ShowDetails
    {
        get => _showDetails;
        private set
        {
            _showDetails = value;
            // Update UI properties
            ShowDetailsButtonText = _showDetails ? "Hide Details" : "Show Details";
            UpdateWindowSize();
        }
    }

    private string _showDetailsButtonText = "Show Details";
    public string ShowDetailsButtonText
    {
        get => _showDetailsButtonText;
        private set => _showDetailsButtonText = value;
    }

    public ErrorDialog()
    {
        InitializeComponent();
        ErrorMessage = string.Empty;
    }

    public ErrorDialog(string errorMessage, Exception? exception = null) : this()
    {
        ErrorMessage = errorMessage;
        
        if (exception != null)
        {
            ExceptionDetails = $"{exception.GetType().Name}: {exception.Message}\n\nStack Trace:\n{exception.StackTrace}";
            HasException = true;
        }
        else
        {
            HasException = false;
        }

        DataContext = this;

        // Set focus to Continue button
        ContinueButton.Focus();

        // Initially hide details
        UpdateWindowSize();
    }

    private void UpdateWindowSize()
    {
        if (ShowDetails && HasException)
        {
            Height = 520; // Expanded height to show details (increased from 500)
        }
        else
        {
            Height = 280; // Compact height without details (increased from 250)
        }
    }

    private void ShowDetailsButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowDetails = !ShowDetails;
        // Force a UI update by updating DataContext
        DataContext = null;
        DataContext = this;
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e)
    {
        UserChoice = ErrorDialogResult.Continue;
        Close(ErrorDialogResult.Continue);
    }

    private void ExitButton_Click(object? sender, RoutedEventArgs e)
    {
        UserChoice = ErrorDialogResult.Exit;
        Close(ErrorDialogResult.Exit);
    }
}
