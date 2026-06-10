using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
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
    private bool _showDebugTools;
    private WavePlayerSettingsProfile _selectedAudioSettingsProfile;
    private BrowserSampleAudioMode _selectedBrowserSampleAudioMode;
    private bool _stopAfterBRKInstruction;
    private bool _stopAfterUnknownInstruction;
    private bool _allowUrlScripts;
    private string _corsProxyUrl;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }

    public EmulatorConfigViewModel(AvaloniaHostApp hostApp, IConfiguration configuration)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _emulatorConfig = hostApp.EmulatorConfig;

        // Initialize available options. The list of selectable systems comes dynamically from the
        // systems registered with the emulator (e.g. C64, Vic20, Generic), sorted for stable display.
        AvailableEmulators = new ObservableCollection<string>(
            _hostApp.AvailableSystemNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        AudioSettingsProfiles = new ObservableCollection<WavePlayerSettingsProfile>(Enum.GetValues<WavePlayerSettingsProfile>());
        BrowserSampleAudioModes = new ObservableCollection<BrowserSampleAudioMode>(Enum.GetValues<BrowserSampleAudioMode>());

        // Initialize working copies from current config
        _selectedDefaultEmulator = _emulatorConfig.DefaultEmulator;
        _defaultDrawScale = _emulatorConfig.DefaultDrawScale;
        _showErrorDialog = _emulatorConfig.ShowErrorDialog;
        _showDebugTools = _emulatorConfig.ShowDebugTools;
        _selectedAudioSettingsProfile = _emulatorConfig.AudioSettingsProfile;
        _selectedBrowserSampleAudioMode = _emulatorConfig.BrowserSampleAudioMode;
        _stopAfterBRKInstruction = _emulatorConfig.Monitor.StopAfterBRKInstruction;
        _stopAfterUnknownInstruction = _emulatorConfig.Monitor.StopAfterUnknownInstruction;
        _allowUrlScripts = _hostApp.ScriptingEngine.AllowUrlScripts;
        _corsProxyUrl = _emulatorConfig.CorsProxyUrl;

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
            outputScheduler: RxSchedulers.MainThreadScheduler);

        CancelCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                ConfigurationChanged?.Invoke(this, false);
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        ResetToDefaultsCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                ResetToDefaults();
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

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

    public bool ShowDebugTools
    {
        get => _showDebugTools;
        set
        {
            if (_showDebugTools == value)
                return;

            this.RaiseAndSetIfChanged(ref _showDebugTools, value);
        }
    }

    // Audio Settings
    public ObservableCollection<WavePlayerSettingsProfile> AudioSettingsProfiles { get; }
    public ObservableCollection<BrowserSampleAudioMode> BrowserSampleAudioModes { get; }

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

    public BrowserSampleAudioMode SelectedBrowserSampleAudioMode
    {
        get => _selectedBrowserSampleAudioMode;
        set
        {
            if (_selectedBrowserSampleAudioMode == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedBrowserSampleAudioMode, value);
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

    // Lua Scripting (read-only, informational)
    public string LuaScriptDirectory => _hostApp.ScriptingEngine.ScriptDirectory;
    public string LuaStorePrefix => _emulatorConfig.LuaStorePrefix;

    /// <summary>
    /// Browser-only knob: when true, the URL <c>script</c> / <c>scriptUrl</c> query parameters
    /// are honoured. Persists to the <see cref="ScriptingConfig.ConfigSectionName"/> section in
    /// browser localStorage. Takes effect on the next page load (the Browser host reads the
    /// value once, before script ingestion).
    /// </summary>
    public bool AllowUrlScripts
    {
        get => _allowUrlScripts;
        set
        {
            if (_allowUrlScripts == value)
                return;

            this.RaiseAndSetIfChanged(ref _allowUrlScripts, value);
        }
    }

    /// <summary>
    /// Browser-only: CORS proxy prefix used to fetch cross-origin resources — system downloads
    /// (games / ROMs) and URL-driven startup (<c>loadPrgUrl</c> / <c>loadD64Url</c> / <c>basicUrl</c>
    /// / <c>scriptUrl</c>). Blank falls back to the built-in default
    /// (<see cref="BrowserServiceDefaults.DefaultCorsProxyUrl"/>). Ignored on desktop.
    /// </summary>
    public string CorsProxyUrl
    {
        get => _corsProxyUrl;
        set
        {
            if (_corsProxyUrl == value)
                return;

            this.RaiseAndSetIfChanged(ref _corsProxyUrl, value);
        }
    }

    public static string CorsProxyUrlWatermark => BrowserServiceDefaults.DefaultCorsProxyUrl;

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

    /// <summary>
    /// Resets all options to application defaults in the working copies. Nothing is persisted until
    /// the user clicks Save. The live <see cref="_emulatorConfig"/> is left untouched here.
    /// </summary>
    private void ResetToDefaults()
    {
        var defaults = new EmulatorConfig();

        SelectedDefaultEmulator = defaults.DefaultEmulator;
        DefaultDrawScale = defaults.DefaultDrawScale;
        ShowErrorDialog = defaults.ShowErrorDialog;
        ShowDebugTools = defaults.ShowDebugTools;
        SelectedAudioSettingsProfile = defaults.AudioSettingsProfile;
        SelectedBrowserSampleAudioMode = defaults.BrowserSampleAudioMode;
        StopAfterBRKInstruction = defaults.Monitor.StopAfterBRKInstruction;
        StopAfterUnknownInstruction = defaults.Monitor.StopAfterUnknownInstruction;
        // Browser-only knob; defaults to disabled.
        AllowUrlScripts = false;
        CorsProxyUrl = defaults.CorsProxyUrl;

        UpdateValidation();
        StatusMessage = "Settings reset to defaults. Click Save to apply.";
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
            _emulatorConfig.ShowDebugTools = _showDebugTools;
            _emulatorConfig.AudioSettingsProfile = _selectedAudioSettingsProfile;
            _emulatorConfig.BrowserSampleAudioMode = _selectedBrowserSampleAudioMode;
            _emulatorConfig.Monitor.StopAfterBRKInstruction = _stopAfterBRKInstruction;
            _emulatorConfig.Monitor.StopAfterUnknownInstruction = _stopAfterUnknownInstruction;
            _emulatorConfig.CorsProxyUrl = _corsProxyUrl;

            // Persist emulator config (note: the _emulatorConfig object is owned by AvaloniaHostApp)
            await _hostApp.PersistEmulatorConfigAsync();

            // Browser-only: persist AllowUrlScripts under the scripting config section. Writes
            // a single-key JSON; other ScriptingConfig values fall back to defaults on next load
            // (which is the existing behaviour — there are no other UI knobs for this section).
            if (IsRunningInWebAssembly && _hostApp.ScriptingEngine.AllowUrlScripts != _allowUrlScripts)
            {
                _hostApp.ScriptingEngine.AllowUrlScripts = _allowUrlScripts;
                var json = $"{{\"{nameof(ScriptingConfig.AllowUrlScripts)}\":{(_allowUrlScripts ? "true" : "false")}}}";
                await _hostApp.PersistConfigStringAsync(ScriptingConfig.ConfigSectionName, json);
            }

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
