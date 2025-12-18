using System;
using System.Reactive;
using Microsoft.Extensions.Logging;
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

    private readonly ILogger _logger;

    public string ErrorMessage { get; }
    public string? ExceptionDetails { get; }
    public bool HasException { get; }

    private bool _showDetails = false;
    public bool ShowDetails
    {
        get => _showDetails;
        private set
        {
            _logger.LogInformation("Setting ShowDetails from {OldValue} to {NewValue}", _showDetails, value);
            this.RaiseAndSetIfChanged(ref _showDetails, value);
            // Update UI properties after the main property change is notified
            ShowDetailsButtonText = _showDetails ? HIDE_DETAILS_BUTTON_TEXT : SHOW_DETAILS_BUTTON_TEXT;
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
        ILoggerFactory loggerFactory,
        string errorMessage,
        Exception? exception)
    {
        _logger = loggerFactory.CreateLogger(typeof(ErrorViewModel).Name);
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

        ShowExceptionDetailsCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                ShowDetails = !ShowDetails;
            },
            outputScheduler: RxApp.MainThreadScheduler);

        ContinueCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                CloseRequested?.Invoke(this, false);
            },
            outputScheduler: RxApp.MainThreadScheduler);

        ExitCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                CloseRequested?.Invoke(this, true);
            },
            outputScheduler: RxApp.MainThreadScheduler);

    }

    public bool IsRunningInWebAssembly { get; } = PlatformDetection.IsRunningInWebAssembly();
}
