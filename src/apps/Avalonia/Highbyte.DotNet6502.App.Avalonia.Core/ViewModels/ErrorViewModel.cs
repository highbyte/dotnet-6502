using System;
using System.Reactive;
 using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class ErrorViewModel : ViewModelBase
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
    public string ShowDetailsButtonText
    {
        get => _showDetailsButtonText;
        private set => _showDetailsButtonText = value;
    }

    private string _showDetailsButtonText = "Show Details";

    public ReactiveCommand<Unit, Unit> ContinueCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    // Event to notify when error dialog should be closed
    public event EventHandler<bool>? CloseRequested;

    public ErrorViewModel(
        string errorMessage,
        Exception? exception)
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

        ContinueCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                CloseRequested?.Invoke(this, false);
            },
            outputScheduler: RxApp.MainThreadScheduler);

        ExitCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                CloseRequested?.Invoke(this, true);
            },
            outputScheduler: RxApp.MainThreadScheduler);

        UpdateWindowSize();
    }

    private void UpdateWindowSize()
    {
        // TODO: How to adjust UserControl size from ViewModel?

        //if (ShowDetails && HasException)
        //{
        //    Height = 520; // Expanded height to show details (increased from 500)
        //}
        //else
        //{
        //    Height = 280; // Compact height without details (increased from 250)
        //}
    }

    public bool IsRunningInWebAssembly { get; } = PlatformDetection.IsRunningInWebAssembly();
}
