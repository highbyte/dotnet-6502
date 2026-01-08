using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Monitor;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class EmulatorConfigViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly MonitorConfig _monitorConfig;

    private bool _isBusy;
    private string? _statusMessage;
    private readonly ObservableCollection<string> _validationErrors = new();

    // Working copies of settings
    private float _defaultDrawScale;
    private bool _showErrorDialog;
    private bool _stopAfterBRKInstruction;
    private bool _stopAfterUnknownInstruction;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public EmulatorConfigViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _emulatorConfig = hostApp.EmulatorConfig;
        _monitorConfig = _emulatorConfig.Monitor;

        // Initialize working copies from current config
        _defaultDrawScale = _emulatorConfig.DefaultDrawScale;
        _showErrorDialog = _emulatorConfig.ShowErrorDialog;
        _stopAfterBRKInstruction = _monitorConfig.StopAfterBRKInstruction;
        _stopAfterUnknownInstruction = _monitorConfig.StopAfterUnknownInstruction;

        // Initialize ReactiveUI Commands with MainThreadScheduler for Browser compatibility
        SaveCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                ApplyChanges();
                ConfigurationChanged?.Invoke(this, true);
            },
            outputScheduler: RxApp.MainThreadScheduler);

        CancelCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                ConfigurationChanged?.Invoke(this, false);
                return Task.CompletedTask;
            },
            outputScheduler: RxApp.MainThreadScheduler);
    }

    public event EventHandler<bool>? ConfigurationChanged;

    public bool IsRunningInWebAssembly { get; } = PlatformDetection.IsRunningInWebAssembly();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            this.RaiseAndSetIfChanged(ref _isBusy, value);
            this.RaisePropertyChanged(nameof(IsNotBusy));
            this.RaisePropertyChanged(nameof(CanSave));
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool CanSave => IsNotBusy && !HasValidationErrors;

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
                return;

            this.RaiseAndSetIfChanged(ref _statusMessage, value);
            this.RaisePropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public ObservableCollection<string> ValidationErrors => _validationErrors;

    public bool HasValidationErrors => _validationErrors.Count > 0;

    public string OkButtonText => IsRunningInWebAssembly ? "Save" : "Ok";

    // Display Settings
    public float DefaultDrawScale
    {
        get => _defaultDrawScale;
        set
        {
            if (Math.Abs(_defaultDrawScale - value) < 0.01f)
                return;

            this.RaiseAndSetIfChanged(ref _defaultDrawScale, value);
            UpdateValidation();
        }
    }

    public bool ShowErrorDialog
    {
        get => _showErrorDialog;
        set
        {
            if (_showErrorDialog == value)
                return;

            this.RaiseAndSetIfChanged(ref _showErrorDialog, value);
        }
    }

    // Monitor Settings
    public bool StopAfterBRKInstruction
    {
        get => _stopAfterBRKInstruction;
        set
        {
            if (_stopAfterBRKInstruction == value)
                return;

            this.RaiseAndSetIfChanged(ref _stopAfterBRKInstruction, value);
        }
    }

    public bool StopAfterUnknownInstruction
    {
        get => _stopAfterUnknownInstruction;
        set
        {
            if (_stopAfterUnknownInstruction == value)
                return;

            this.RaiseAndSetIfChanged(ref _stopAfterUnknownInstruction, value);
        }
    }

    private void UpdateValidation()
    {
        _validationErrors.Clear();

        if (_defaultDrawScale < 1.0f || _defaultDrawScale > 4.0f)
        {
            _validationErrors.Add("Default draw scale must be between 1.0 and 4.0");
        }

        this.RaisePropertyChanged(nameof(HasValidationErrors));
        this.RaisePropertyChanged(nameof(CanSave));
    }

    private void ApplyChanges()
    {
        // Apply settings to config objects (in-memory only, no persistence)
        _emulatorConfig.DefaultDrawScale = _defaultDrawScale;
        _emulatorConfig.ShowErrorDialog = _showErrorDialog;
        _monitorConfig.StopAfterBRKInstruction = _stopAfterBRKInstruction;
        _monitorConfig.StopAfterUnknownInstruction = _stopAfterUnknownInstruction;

        StatusMessage = "Settings applied.";
    }
}
