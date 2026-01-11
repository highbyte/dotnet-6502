using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers;
using Highbyte.DotNet6502.Monitor;
using Microsoft.Extensions.Configuration;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class EmulatorConfigViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly IConfiguration _configuration;
    private readonly EmulatorConfig _emulatorConfig;

    private bool _isBusy;
    private string? _statusMessage;
    private readonly ObservableCollection<string> _validationErrors = new();

    // Working copies of settings
    private string _selectedDefaultEmulator;
    private float _defaultDrawScale;
    private bool _showErrorDialog;
    private bool _showDebugTab;
    private WavePlayerSettingsProfile _selectedAudioSettingsProfile;
    private bool _stopAfterBRKInstruction;
    private bool _stopAfterUnknownInstruction;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public EmulatorConfigViewModel(AvaloniaHostApp hostApp, IConfiguration configuration)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _emulatorConfig = hostApp.EmulatorConfig;

        // Initialize available options
        AvailableEmulators = new ObservableCollection<string> { "C64", "Generic" };
        AudioSettingsProfiles = new ObservableCollection<WavePlayerSettingsProfile>(Enum.GetValues<WavePlayerSettingsProfile>());

        // Initialize working copies from current config
        _selectedDefaultEmulator = _emulatorConfig.DefaultEmulator;
        _defaultDrawScale = _emulatorConfig.DefaultDrawScale;
        _showErrorDialog = _emulatorConfig.ShowErrorDialog;
        _showDebugTab = _emulatorConfig.ShowDebugTab;
        _selectedAudioSettingsProfile = _emulatorConfig.AudioSettingsProfile;
        _stopAfterBRKInstruction = _emulatorConfig.Monitor.StopAfterBRKInstruction;
        _stopAfterUnknownInstruction = _emulatorConfig.Monitor.StopAfterUnknownInstruction;

        // Initialize ReactiveUI Commands with MainThreadScheduler for Browser compatibility
        SaveCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                var success = await TryApplyChangesAsync();
                if (success)
                {
                    ConfigurationChanged?.Invoke(this, true);
                }
            },
            outputScheduler: RxApp.MainThreadScheduler);

        CancelCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                ConfigurationChanged?.Invoke(this, false);
                return Task.CompletedTask;
            },
            outputScheduler: RxApp.MainThreadScheduler);

        // Show info message on desktop about permanent settings
        if (!IsRunningInWebAssembly)
        {
            StatusMessage = "To change the settings permanently, update the appsettings.json file.";
        }
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

    // Default Emulator Setting
    public ObservableCollection<string> AvailableEmulators { get; }

    public string SelectedDefaultEmulator
    {
        get => _selectedDefaultEmulator;
        set
        {
            if (_selectedDefaultEmulator == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedDefaultEmulator, value);
        }
    }

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

    public bool ShowDebugTab
    {
        get => _showDebugTab;
        set
        {
            if (_showDebugTab == value)
                return;

            this.RaiseAndSetIfChanged(ref _showDebugTab, value);
        }
    }

    // Audio Settings
    public ObservableCollection<WavePlayerSettingsProfile> AudioSettingsProfiles { get; }

    public WavePlayerSettingsProfile SelectedAudioSettingsProfile
    {
        get => _selectedAudioSettingsProfile;
        set
        {
            if (_selectedAudioSettingsProfile == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedAudioSettingsProfile, value);
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

    private async Task<bool> TryApplyChangesAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Saving...";
            _validationErrors.Clear();
            this.RaisePropertyChanged(nameof(HasValidationErrors));

            // Apply settings to config objects
            _emulatorConfig.DefaultEmulator = _selectedDefaultEmulator;
            _emulatorConfig.DefaultDrawScale = _defaultDrawScale;
            _emulatorConfig.ShowErrorDialog = _showErrorDialog;
            _emulatorConfig.ShowDebugTab = _showDebugTab;
            _emulatorConfig.AudioSettingsProfile = _selectedAudioSettingsProfile;
            _emulatorConfig.Monitor.StopAfterBRKInstruction = _stopAfterBRKInstruction;
            _emulatorConfig.Monitor.StopAfterUnknownInstruction = _stopAfterUnknownInstruction;

            // Persist emulator config (note: the _emulatorConfig object is owned by AvaloniaHostApp)
            await _hostApp.PersistEmulatorConfig();

            StatusMessage = "Configuration saved.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving config: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
