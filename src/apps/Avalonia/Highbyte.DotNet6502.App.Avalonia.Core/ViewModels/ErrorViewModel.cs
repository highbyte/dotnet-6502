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
            ShowDetailsButtonText = _showDetails ? HIDE_DETAILS_BUTTON_TEXT : SHOW_DETAILS_BUTTON_TEXT;
            this.RaisePropertyChanged(nameof(ShowDetails));
        }
    }
    public string ShowDetailsButtonText
    {
        get => _showDetailsButtonText;
        private set
        {
            _showDetailsButtonText = value;
            this.RaisePropertyChanged(nameof(ShowDetailsButtonText));
        }
    }

    private const string HIDE_DETAILS_BUTTON_TEXT = "Hide Details";
    private const string SHOW_DETAILS_BUTTON_TEXT = "Show Details";

    private string _showDetailsButtonText = SHOW_DETAILS_BUTTON_TEXT;

    public ReactiveCommand<Unit, Unit> ShowExceptionDetailsCommand { get; }
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

        ShowExceptionDetailsCommand = ReactiveCommand.Create(
            () =>
            {
                ShowDetails = !ShowDetails;
            },
            outputScheduler: RxApp.MainThreadScheduler);

        ContinueCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                CloseRequested?.Invoke(this, false);
            },
            outputScheduler: RxApp.MainThreadScheduler);

        ExitCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                CloseRequested?.Invoke(this, true);
            },
            outputScheduler: RxApp.MainThreadScheduler);

    }

    public bool IsRunningInWebAssembly { get; } = PlatformDetection.IsRunningInWebAssembly();
}
