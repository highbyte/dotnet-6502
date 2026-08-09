using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Apple2;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Utils;
using Highbyte.DotNet6502.Utils;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Apple2.ViewModels;

/// <summary>
/// Apple II configuration dialog: ROM files, ROM directory, monitor colour, render provider and
/// CPU compatibility profile.
/// </summary>
public class Apple2ConfigDialogViewModel : ViewModelBase
{
    /// <summary>The largest Apple II ROM is the 20 KB $B000-$FFFF system image layout.</summary>
    private const long MaxRomFileSizeBytes = 32 * 1024;

    // Keyboard layout dropdown entry meaning "no explicit setting" -> null config -> auto-detect.
    private const string AutoKeyboardLayoutLabel = "Auto";

    private readonly AvaloniaHostApp _hostApp;
    private readonly Apple2HostConfig _originalConfig;
    private Apple2HostConfig _workingConfig;
    private readonly List<(Type renderProviderType, Type renderTargetType)> _renderCombinations;
    private readonly List<(Type audioProviderType, Type audioTargetType)> _audioCombinations;
    private readonly HttpClient _httpClient;
    private readonly ObservableCollection<string> _validationErrors = new();

    private bool _isBusy;
    private string? _statusMessage;
    private bool _statusMessageIsError;
    private string? _validationMessage;
    private string _romDirectory = string.Empty;
    private RenderProviderOption? _selectedRenderProvider;
    private RenderTargetOption? _selectedRenderTarget;
    private bool _suppressRenderTargetUpdate;
    private CpuCompatibilityProfileOption? _selectedCpuCompatibilityProfile;
    private MonitorColorOption? _selectedMonitorColor;
    private string _selectedKeyboardLayout = AutoKeyboardLayoutLabel;

    public ReactiveCommand<Unit, Unit> DownloadRomsToByteArrayCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadRomsToFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearRomsCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public Apple2ConfigDialogViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _originalConfig = hostApp.CurrentHostSystemConfig as Apple2HostConfig
            ?? throw new Exception("hostApp.CurrentHostSystemConfig must be type Apple2HostConfig");
        _renderCombinations = hostApp.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations() ?? new List<(Type, Type)>();
        _audioCombinations = hostApp.GetAvailableSystemAudioProviderTypesAndAudioTargetTypeCombinations() ?? new List<(Type, Type)>();
        _workingConfig = (Apple2HostConfig)_originalConfig.Clone();
        _httpClient = new HttpClient();

        LoadFromWorkingConfig();

        DownloadRomsToByteArrayCommand = ReactiveCommandHelper.CreateSafeCommand(
            AutoDownloadRomsToByteArrayAsync,
            outputScheduler: RxSchedulers.MainThreadScheduler);

        DownloadRomsToFilesCommand = ReactiveCommandHelper.CreateSafeCommand(
            AutoDownloadROMsToFilesAsync,
            outputScheduler: RxSchedulers.MainThreadScheduler);

        ClearRomsCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                UnloadRoms();
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

        SaveCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                if (await TryApplyChangesAsync())
                    ConfigurationChanged?.Invoke(this, true);
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        CancelCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                ConfigurationChanged?.Invoke(this, false);
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);
    }

    public event EventHandler<bool>? ConfigurationChanged;
    public event EventHandler<Apple2RomLicenseAcknowledgementEventArgs>? RomLicenseAcknowledgementRequested;

    public ObservableCollection<Apple2RomStatusViewModel> RomStatuses { get; } = new();
    public ObservableCollection<RenderProviderOption> RenderProviders { get; } = new();
    public ObservableCollection<AudioProviderOption> AudioProviders { get; } = new();
    public ObservableCollection<AudioTargetOption> AudioTargets { get; } = new();
    public ObservableCollection<RenderTargetOption> RenderTargets { get; } = new();
    public ObservableCollection<CpuCompatibilityProfileOption> CpuCompatibilityProfiles { get; } =
        new(CpuCompatibilityProfileOption.All);
    public ObservableCollection<MonitorColorOption> MonitorColors { get; } = new(MonitorColorOption.All);
    // "Auto" (auto-detect) plus each explicit HostKeyboardLayout, as strings for the dropdown.
    public ObservableCollection<string> AvailableKeyboardLayouts { get; } =
        new(new[] { AutoKeyboardLayoutLabel }.Concat(Enum.GetNames<HostKeyboardLayout>()));

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
            this.RaisePropertyChanged(nameof(HasNonErrorStatusMessage));
            this.RaisePropertyChanged(nameof(HasErrorStatusMessage));
        }
    }

    public bool StatusMessageIsError
    {
        get => _statusMessageIsError;
        private set
        {
            if (_statusMessageIsError == value)
                return;

            this.RaiseAndSetIfChanged(ref _statusMessageIsError, value);
            this.RaisePropertyChanged(nameof(HasNonErrorStatusMessage));
            this.RaisePropertyChanged(nameof(HasErrorStatusMessage));
        }
    }

    private void SetStatusMessage(string? message, bool isError = false)
    {
        StatusMessage = message;
        StatusMessageIsError = !string.IsNullOrEmpty(message) && isError;
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (_validationMessage == value)
                return;

            this.RaiseAndSetIfChanged(ref _validationMessage, value);
            this.RaisePropertyChanged(nameof(HasValidationMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);
    public bool HasNonErrorStatusMessage => HasStatusMessage && !StatusMessageIsError;
    public bool HasErrorStatusMessage => HasStatusMessage && StatusMessageIsError;
    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);
    public ObservableCollection<string> ValidationErrors => _validationErrors;
    public bool HasValidationErrors => _validationErrors.Count > 0;

    public string RomStatusSummary =>
        $"{RomStatuses.Count(r => r.IsRequired && r.IsLoaded)}/{Apple2SystemConfig.RequiredROMs.Count} ROMs loaded";

    public string RomDirectory
    {
        get => _romDirectory;
        set
        {
            if (_romDirectory == value)
                return;

            this.RaiseAndSetIfChanged(ref _romDirectory, value);
            _workingConfig.SystemConfig.ROMDirectory = value;
            this.RaisePropertyChanged(nameof(EffectiveRomDirectory));
            UpdateValidationMessageFromConfig();
        }
    }

    public string EffectiveRomDirectory =>
        PathHelper.ExpandOSEnvironmentVariables(_workingConfig.SystemConfig.EffectiveROMDirectory);

    public string RomDirectoryToolTip =>
        $"Optional ROM directory override. Leave blank to use the default: {PathHelper.ExpandOSEnvironmentVariables(Apple2SystemConfig.DefaultROMDirectory)}";

    public string RomSourceUrl => Apple2SystemConfig.ROM_SOURCE_INFO_URL;

    public RenderProviderOption? SelectedRenderProvider
    {
        get => _selectedRenderProvider;
        set
        {
            if (ReferenceEquals(_selectedRenderProvider, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedRenderProvider, value);

            if (value != null)
            {
                _workingConfig.SystemConfig.SetRenderProviderType(value.Type);
                UpdateRenderTargetsForProvider(value.Type);
            }

            this.RaisePropertyChanged(nameof(SelectedRenderProviderHelpText));
        }
    }

    public RenderTargetOption? SelectedRenderTarget
    {
        get => _selectedRenderTarget;
        set
        {
            if (ReferenceEquals(_selectedRenderTarget, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedRenderTarget, value);

            if (value != null && !_suppressRenderTargetUpdate)
                _workingConfig.SystemConfig.SetRenderTargetType(value.Type);

            this.RaisePropertyChanged(nameof(SelectedRenderTargetHelpText));
        }
    }

    public string SelectedRenderProviderHelpText => SelectedRenderProvider?.HelpText ?? string.Empty;

    private AudioProviderOption? _selectedAudioProvider;
    public AudioProviderOption? SelectedAudioProvider
    {
        get => _selectedAudioProvider;
        set
        {
            if (ReferenceEquals(_selectedAudioProvider, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedAudioProvider, value);

            if (value != null)
            {
                _workingConfig.SystemConfig.SetAudioProviderType(value.Type);
                UpdateAudioTargetsForProvider(value.Type);
            }

            this.RaisePropertyChanged(nameof(SelectedAudioProviderHelpText));
        }
    }

    private bool _suppressAudioTargetUpdate;
    private AudioTargetOption? _selectedAudioTarget;
    public AudioTargetOption? SelectedAudioTarget
    {
        get => _selectedAudioTarget;
        set
        {
            if (ReferenceEquals(_selectedAudioTarget, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedAudioTarget, value);

            if (value != null && !_suppressAudioTargetUpdate)
                _workingConfig.SystemConfig.SetAudioTargetType(value.Type);

            this.RaisePropertyChanged(nameof(SelectedAudioTargetHelpText));
        }
    }

    public string SelectedAudioProviderHelpText => SelectedAudioProvider?.HelpText ?? string.Empty;
    public string SelectedAudioTargetHelpText => SelectedAudioTarget?.HelpText ?? string.Empty;

    /// <summary>Whether the speaker is emulated at all.</summary>
    public bool AudioEnabled
    {
        get => _workingConfig.SystemConfig.AudioEnabled;
        set
        {
            if (_workingConfig.SystemConfig.AudioEnabled == value)
                return;
            _workingConfig.SystemConfig.AudioEnabled = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Whether the 16 KB language card is fitted, taking the machine to 64 KB. Off gives a stock
    /// 48 KB Apple II Plus, which cannot run ProDOS software.
    /// </summary>
    public bool LanguageCardEnabled
    {
        get => _workingConfig.SystemConfig.LanguageCardEnabled;
        set
        {
            if (_workingConfig.SystemConfig.LanguageCardEnabled == value)
                return;
            _workingConfig.SystemConfig.LanguageCardEnabled = value;
            this.RaisePropertyChanged();
        }
    }

    public string SelectedRenderTargetHelpText => SelectedRenderTarget?.HelpText ?? string.Empty;

    public CpuCompatibilityProfileOption? SelectedCpuCompatibilityProfile
    {
        get => _selectedCpuCompatibilityProfile;
        set
        {
            if (ReferenceEquals(_selectedCpuCompatibilityProfile, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedCpuCompatibilityProfile, value);

            if (value != null)
                _workingConfig.SystemConfig.CpuCompatibilityProfile = value.Profile;

            this.RaisePropertyChanged(nameof(SelectedCpuCompatibilityProfileHelpText));
        }
    }

    public string SelectedCpuCompatibilityProfileHelpText => SelectedCpuCompatibilityProfile?.HelpText ?? string.Empty;

    public MonitorColorOption? SelectedMonitorColor
    {
        get => _selectedMonitorColor;
        set
        {
            if (ReferenceEquals(_selectedMonitorColor, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedMonitorColor, value);

            if (value != null)
                _workingConfig.SystemConfig.MonitorColor = value.MonitorColor;

            this.RaisePropertyChanged(nameof(SelectedMonitorColorHelpText));
        }
    }

    public string SelectedMonitorColorHelpText => SelectedMonitorColor?.HelpText ?? string.Empty;

    /// <summary>
    /// The host keyboard layout the Apple II keyboard mapping assumes, as a dropdown string.
    /// <see cref="AutoKeyboardLayoutLabel"/> means "no explicit setting" — a null config value,
    /// which makes the input handler auto-detect.
    /// </summary>
    public string SelectedKeyboardLayout
    {
        get => _selectedKeyboardLayout;
        set
        {
            if (_selectedKeyboardLayout == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedKeyboardLayout, value);

            _workingConfig.InputConfig.KeyboardLayout =
                string.IsNullOrEmpty(value) || value == AutoKeyboardLayoutLabel
                    ? null
                    : Enum.Parse<HostKeyboardLayout>(value);
        }
    }

    /// <summary>
    /// Whether host keys drive the game port. Edits the working copy like every other setting in
    /// this dialog, so Cancel discards it; the sidebar checkbox is the live counterpart for
    /// toggling it while a game is running.
    /// </summary>
    public bool KeyboardJoystickEnabled
    {
        get => _workingConfig.SystemConfig.KeyboardJoystickEnabled;
        set
        {
            if (_workingConfig.SystemConfig.KeyboardJoystickEnabled == value)
                return;
            _workingConfig.SystemConfig.KeyboardJoystickEnabled = value;
            this.RaisePropertyChanged();
        }
    }

    public string OkButtonText => IsRunningInWebAssembly ? "Save" : "Ok";

    /// <summary>
    /// Builds the shared ROM downloader. Supplying the host's download cache means a repeated
    /// download is served locally, and the CORS proxy keeps the browser host working.
    /// </summary>
    private RomDownloader CreateRomDownloader()
        => new(
            _hostApp.LoggerFactory,
            _httpClient,
            _hostApp.GetCorsProxyUrl(),
            _hostApp.GetDownloadCache());

    public Task AutoDownloadRomsToByteArrayAsync()
        => DownloadRomsToByteArrayAsync(requireAcknowledgement: true);

    public async Task<bool> DownloadRomsToByteArrayAsync(bool requireAcknowledgement)
    {
        if (requireAcknowledgement && !await RequestRomLicenseAcknowledgementAsync())
        {
            SetStatusMessage("ROM download cancelled.");
            return false;
        }

        try
        {
            IsBusy = true;
            SetStatusMessage("Downloading ROMs...");
            ValidationMessage = string.Empty;

            var downloadedRoms = await CreateRomDownloader()
                .DownloadRomsAsync(_workingConfig.SystemConfig.ROMDownloadSources);
            foreach (var (romName, romBytes) in downloadedRoms)
                _workingConfig.SystemConfig.SetROM(romName, data: romBytes);

            SetStatusMessage("ROMs downloaded successfully.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatusMessage($"Error downloading ROMs: {ex.Message}", isError: true);
            return false;
        }
        finally
        {
            IsBusy = false;
            UpdateRomStatuses();
            UpdateValidationMessageFromConfig();
        }
    }

    public async Task AutoDownloadROMsToFilesAsync()
    {
        if (!await RequestRomLicenseAcknowledgementAsync())
        {
            SetStatusMessage(string.Empty);
            return;
        }

        try
        {
            IsBusy = true;
            SetStatusMessage("Downloading ROMs...");
            ValidationMessage = string.Empty;

            var writtenFiles = await CreateRomDownloader().DownloadRomsToFilesAsync(
                _workingConfig.SystemConfig.ROMDownloadSources,
                _workingConfig.SystemConfig.EffectiveROMDirectory);

            foreach (var (romName, fileName) in writtenFiles)
                _workingConfig.SystemConfig.SetROM(romName, fileName);

            SetStatusMessage("ROMs downloaded successfully.");
        }
        catch (Exception ex)
        {
            SetStatusMessage($"Error downloading ROMs: {ex.Message}", isError: true);
        }
        finally
        {
            IsBusy = false;
            UpdateRomStatuses();
            UpdateValidationMessageFromConfig();
        }
    }

    public Task LoadRomsFromDataAsync(IEnumerable<(string fileName, byte[] data)> romDataList)
    {
        if (romDataList == null)
            return Task.CompletedTask;

        var romData = romDataList.ToList();
        var errors = new List<string>();
        foreach (var (fileName, data) in romData)
        {
            try
            {
                if (data.Length > MaxRomFileSizeBytes)
                {
                    errors.Add($"File {fileName} is larger than {MaxRomFileSizeBytes} bytes.");
                    continue;
                }

                var romName = DetectRomName(fileName, data.Length);
                if (romName == null)
                {
                    errors.Add(
                        $"Could not determine ROM type for file {fileName}. Expected a name containing " +
                        $"'apple' or 'char'/'3410036', or a 2 KB character generator / 12 KB system ROM.");
                    continue;
                }

                _workingConfig.SystemConfig.SetROM(romName, data: data);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to load {fileName}: {ex.Message}");
            }
        }

        UpdateRomStatuses();
        UpdateValidationMessageFromConfig();

        if (errors.Count == 0)
        {
            SetStatusMessage("ROM files loaded.");
            ValidationMessage = string.Empty;
        }
        else
        {
            SetStatusMessage(errors.Count < romData.Count ? "Some ROMs loaded with warnings." : null, isError: errors.Count < romData.Count);
            ValidationMessage = string.Join(Environment.NewLine, errors);
        }

        return Task.CompletedTask;
    }

    public void UnloadRoms()
    {
        _workingConfig.SystemConfig.ROMs = new List<ROM>();
        UpdateRomStatuses();
        UpdateValidationMessageFromConfig();
        SetStatusMessage("All ROMs cleared.");
    }

    public async Task<bool> TryApplyChangesAsync()
    {
        try
        {
            IsBusy = true;
            SetStatusMessage("Saving...");
            ValidationMessage = string.Empty;

            if (!_workingConfig.IsValid(out var validationErrors))
            {
                SetStatusMessage(null);
                ValidationMessage = string.Join(Environment.NewLine, validationErrors);
                return false;
            }

            _originalConfig.SystemConfig.ROMDirectory = _workingConfig.SystemConfig.ROMDirectory;
            _originalConfig.SystemConfig.ROMs = ROM.Clone(_workingConfig.SystemConfig.ROMs);
            _originalConfig.SystemConfig.CpuCompatibilityProfile = _workingConfig.SystemConfig.CpuCompatibilityProfile;
            _originalConfig.SystemConfig.MonitorColor = _workingConfig.SystemConfig.MonitorColor;
            _originalConfig.SystemConfig.KeyboardJoystickEnabled = _workingConfig.SystemConfig.KeyboardJoystickEnabled;
            _originalConfig.InputConfig.KeyboardLayout = _workingConfig.InputConfig.KeyboardLayout;
            _originalConfig.SystemConfig.AudioEnabled = _workingConfig.SystemConfig.AudioEnabled;
            _originalConfig.SystemConfig.LanguageCardEnabled = _workingConfig.SystemConfig.LanguageCardEnabled;
            _originalConfig.SystemConfig.SetAudioProviderType(_workingConfig.SystemConfig.AudioProviderType);
            _originalConfig.SystemConfig.SetAudioTargetType(_workingConfig.SystemConfig.AudioTargetType);

            if (_workingConfig.SystemConfig.RenderProviderType != null)
                _originalConfig.SystemConfig.SetRenderProviderType(_workingConfig.SystemConfig.RenderProviderType);

            if (_workingConfig.SystemConfig.RenderTargetType != null)
                _originalConfig.SystemConfig.SetRenderTargetType(_workingConfig.SystemConfig.RenderTargetType);

            _hostApp.UpdateHostSystemConfig(_originalConfig);
            await _hostApp.PersistCurrentHostSystemConfig();

            SetStatusMessage("Configuration saved.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatusMessage($"Error saving config: {ex.Message}", isError: true);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Populates all bound view-model properties from the current <see cref="_workingConfig"/>.
    /// Called from the constructor and after <see cref="ResetToDefaults"/> swaps the working config.
    /// </summary>
    private void LoadFromWorkingConfig()
    {
        RomDirectory = _workingConfig.SystemConfig.ROMDirectory;
        SelectedCpuCompatibilityProfile = CpuCompatibilityProfileOption.FromProfile(_workingConfig.SystemConfig.CpuCompatibilityProfile);
        SelectedMonitorColor = MonitorColorOption.FromMonitorColor(_workingConfig.SystemConfig.MonitorColor);
        SelectedKeyboardLayout = _workingConfig.InputConfig.KeyboardLayout?.ToString() ?? AutoKeyboardLayoutLabel;

        InitializeRenderOptions();
        InitializeAudioOptions();
        UpdateRomStatuses();
        UpdateValidationMessageFromConfig();
    }

    /// <summary>
    /// Resets all settings to application defaults, while preserving the user's loaded ROMs and ROM
    /// directory (so they don't have to re-download or re-point ROM files). Nothing is persisted until
    /// the user clicks Save.
    /// </summary>
    private void ResetToDefaults()
    {
        var preservedRoms = ROM.Clone(_workingConfig.SystemConfig.ROMs);
        var preservedRomDirectory = _workingConfig.SystemConfig.ROMDirectory;

        _workingConfig = new Apple2HostConfig();
        _workingConfig.SystemConfig.ROMs = preservedRoms;
        _workingConfig.SystemConfig.ROMDirectory = preservedRomDirectory;

        LoadFromWorkingConfig();

        SetStatusMessage("Settings reset to defaults. Click Save to apply.");
    }

    private void InitializeRenderOptions()
    {
        RenderProviders.Clear();

        var providerTypes = _renderCombinations.Select(c => c.renderProviderType).Distinct().ToList();
        if (_workingConfig.SystemConfig.RenderProviderType != null && !providerTypes.Contains(_workingConfig.SystemConfig.RenderProviderType))
            providerTypes.Add(_workingConfig.SystemConfig.RenderProviderType);

        foreach (var providerType in providerTypes)
        {
            RenderProviders.Add(new RenderProviderOption(
                providerType,
                TypeDisplayHelper.GetDisplayName(providerType),
                TypeDisplayHelper.GetHelpText(providerType)));
        }

        SelectedRenderProvider = RenderProviders.FirstOrDefault(rp => rp.Type == _workingConfig.SystemConfig.RenderProviderType)
            ?? RenderProviders.FirstOrDefault();

        if (SelectedRenderProvider != null)
            _workingConfig.SystemConfig.SetRenderProviderType(SelectedRenderProvider.Type);
    }

    /// <summary>
    /// Populates the audio provider/target lists and picks a default. Without this the pipeline has
    /// nothing selected and stays silent no matter how the enable checkbox is set — audio needs
    /// both halves chosen, not just a provider that exists.
    /// </summary>
    private void InitializeAudioOptions()
    {
        AudioProviders.Clear();

        var providerTypes = _audioCombinations.Select(c => c.audioProviderType).Distinct().ToList();
        foreach (var providerType in providerTypes)
        {
            AudioProviders.Add(new AudioProviderOption(
                providerType,
                TypeDisplayHelper.GetDisplayName(providerType),
                TypeDisplayHelper.GetHelpText(providerType)));
        }

        SelectedAudioProvider = AudioProviders.FirstOrDefault(ap => ap.Type == _workingConfig.SystemConfig.AudioProviderType)
            ?? AudioProviders.FirstOrDefault();

        if (SelectedAudioProvider != null)
            _workingConfig.SystemConfig.SetAudioProviderType(SelectedAudioProvider.Type);
    }

    private void UpdateAudioTargetsForProvider(Type providerType)
    {
        try
        {
            _suppressAudioTargetUpdate = true;
            AudioTargets.Clear();

            var targetTypes = _audioCombinations
                .Where(c => c.audioProviderType == providerType)
                .Select(c => c.audioTargetType)
                .Distinct()
                .ToList();

            foreach (var targetType in targetTypes)
            {
                AudioTargets.Add(new AudioTargetOption(
                    targetType,
                    TypeDisplayHelper.GetDisplayName(targetType),
                    TypeDisplayHelper.GetHelpText(targetType)));
            }

            SelectedAudioTarget = AudioTargets.FirstOrDefault(at => at.Type == _workingConfig.SystemConfig.AudioTargetType)
                ?? AudioTargets.FirstOrDefault();

            if (SelectedAudioTarget != null)
                _workingConfig.SystemConfig.SetAudioTargetType(SelectedAudioTarget.Type);
        }
        finally
        {
            _suppressAudioTargetUpdate = false;
        }
    }

    private void UpdateRenderTargetsForProvider(Type providerType)
    {
        try
        {
            _suppressRenderTargetUpdate = true;
            RenderTargets.Clear();

            var targetTypes = _renderCombinations
                .Where(c => c.renderProviderType == providerType)
                .Select(c => c.renderTargetType)
                .Distinct()
                .ToList();

            foreach (var targetType in targetTypes)
            {
                RenderTargets.Add(new RenderTargetOption(
                    targetType,
                    TypeDisplayHelper.GetDisplayName(targetType),
                    TypeDisplayHelper.GetHelpText(targetType)));
            }

            SelectedRenderTarget = RenderTargets.FirstOrDefault(rt => rt.Type == _workingConfig.SystemConfig.RenderTargetType)
                ?? RenderTargets.FirstOrDefault();

            if (SelectedRenderTarget != null)
                _workingConfig.SystemConfig.SetRenderTargetType(SelectedRenderTarget.Type);
        }
        finally
        {
            _suppressRenderTargetUpdate = false;
        }
    }

    private void UpdateRomStatuses()
    {
        var roms = _workingConfig.SystemConfig.ROMs;
        var desiredStatuses = new List<Apple2RomStatusData>();

        foreach (var required in Apple2SystemConfig.RequiredROMs)
        {
            var rom = roms.FirstOrDefault(r => string.Equals(r.Name, required, StringComparison.OrdinalIgnoreCase));
            desiredStatuses.Add(CreateRomStatusData(required, rom, isRequired: true));
        }

        foreach (var additional in roms.Where(r => !Apple2SystemConfig.RequiredROMs.Contains(r.Name)))
        {
            desiredStatuses.Add(CreateRomStatusData(additional.Name, additional, isRequired: false));
        }

        var existingByName = RomStatuses.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < desiredStatuses.Count; i++)
        {
            var statusData = desiredStatuses[i];
            if (existingByName.TryGetValue(statusData.Name, out var existing))
            {
                existing.UpdateFromData(statusData);
                var currentIndex = RomStatuses.IndexOf(existing);
                if (currentIndex != i)
                    RomStatuses.Move(currentIndex, i);
                existingByName.Remove(statusData.Name);
            }
            else
            {
                var romName = statusData.Name;
                RomStatuses.Insert(i, new Apple2RomStatusViewModel(
                    romName,
                    statusData.IsLoaded,
                    statusData.IsRequired,
                    statusData.Details,
                    statusData.ForegroundColor,
                    statusData.RomFile,
                    filePath => UpdateRomFile(romName, filePath),
                    IsRunningInWebAssembly));
            }
        }

        foreach (var obsolete in existingByName.Values)
            RomStatuses.Remove(obsolete);

        this.RaisePropertyChanged(nameof(RomStatuses));
        this.RaisePropertyChanged(nameof(RomStatusSummary));
    }

    private Apple2RomStatusData CreateRomStatusData(string romName, ROM? rom, bool isRequired)
    {
        var romFile = rom?.File ?? string.Empty;
        var romDataLength = rom?.Data?.Length ?? 0;
        var hasData = romDataLength > 0;
        var hasFile = !string.IsNullOrWhiteSpace(romFile);

        var fileExists = false;
        if (hasFile && rom != null)
        {
            try
            {
                var romFilePath = rom.GetROMFilePath(_workingConfig.SystemConfig.EffectiveROMDirectory);
                fileExists = File.Exists(romFilePath);
            }
            catch
            {
                var expandedPath = PathHelper.ExpandOSEnvironmentVariables(romFile);
                fileExists = File.Exists(expandedPath);
            }
        }

        var isLoaded = IsRunningInWebAssembly
            ? hasData
            : hasData || (hasFile && fileExists);

        var details = IsRunningInWebAssembly
            ? hasData ? $"{romDataLength} bytes" : "Not loaded"
            : !hasFile ? "ROM file not set" : fileExists ? romFile : $"{romFile} (missing)";

        var foregroundColor = IsRunningInWebAssembly
            ? isLoaded ? "#68D391" : "#F56565"
            : !hasFile ? "#F56565" : fileExists ? "#68D391" : "#F6AD55";

        return new Apple2RomStatusData(romName, isLoaded, isRequired, details, foregroundColor, romFile);
    }

    private void UpdateRomFile(string romName, string romFile)
    {
        var trimmedFile = string.IsNullOrWhiteSpace(romFile) ? null : romFile.Trim();
        ROM? existingRom = null;
        if (_workingConfig.SystemConfig.HasROM(romName))
            existingRom = _workingConfig.SystemConfig.GetROM(romName);

        if (trimmedFile != null)
            _workingConfig.SystemConfig.SetROM(romName, file: trimmedFile, data: null);
        else
            _workingConfig.SystemConfig.SetROM(romName, file: null, data: existingRom?.Data);

        UpdateRomStatuses();
        UpdateValidationMessageFromConfig();
    }

    private void UpdateValidationMessageFromConfig()
    {
        if (!_workingConfig.IsValid(out var validationErrors))
        {
            ValidationMessage = string.Join(Environment.NewLine, validationErrors);
            _validationErrors.Clear();
            foreach (var error in validationErrors)
                _validationErrors.Add(error);
        }
        else
        {
            ValidationMessage = string.Empty;
            _validationErrors.Clear();
        }

        this.RaisePropertyChanged(nameof(HasValidationErrors));
        this.RaisePropertyChanged(nameof(CanSave));
    }

    /// <summary>
    /// Works out which ROM a picked file is. Falls back to size, because the Apple II ROMs
    /// have unmistakably different sizes and the archive's file names (<c>apple.rom</c>,
    /// <c>3410036.BIN</c>) share no obvious keyword. "disk"/"0027" is checked before "apple"
    /// because the archive's Disk II ROM file name contains both words.
    /// </summary>
    internal static string? DetectRomName(string fileName, int byteLength)
    {
        if (fileName.Contains("char", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("chargen", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("3410036", StringComparison.OrdinalIgnoreCase))
            return Apple2SystemConfig.CHARGEN_ROM_NAME;

        if (fileName.Contains("disk", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("0027", StringComparison.OrdinalIgnoreCase))
            return Apple2SystemConfig.DISK2_ROM_NAME;

        if (fileName.Contains("apple", StringComparison.OrdinalIgnoreCase))
            return Apple2SystemConfig.SYSTEM_ROM_NAME;

        return byteLength switch
        {
            Highbyte.DotNet6502.Systems.Apple2.Apple2.SystemRomSize => Apple2SystemConfig.SYSTEM_ROM_NAME,
            20480 => Apple2SystemConfig.SYSTEM_ROM_NAME,
            2048 or 512 => Apple2SystemConfig.CHARGEN_ROM_NAME,
            Highbyte.DotNet6502.Systems.Apple2.Disk2.Disk2Controller.BootRomSize => Apple2SystemConfig.DISK2_ROM_NAME,
            _ => null,
        };
    }

    private Task<bool> RequestRomLicenseAcknowledgementAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        var args = new Apple2RomLicenseAcknowledgementEventArgs(tcs);
        RomLicenseAcknowledgementRequested?.Invoke(this, args);
        return tcs.Task;
    }
}

public class Apple2RomLicenseAcknowledgementEventArgs : EventArgs
{
    private readonly TaskCompletionSource<bool> _taskCompletionSource;

    public Apple2RomLicenseAcknowledgementEventArgs(TaskCompletionSource<bool> taskCompletionSource)
    {
        _taskCompletionSource = taskCompletionSource;
    }

    public void SetResult(bool acknowledged) => _taskCompletionSource.TrySetResult(acknowledged);
}

public class Apple2RomStatusViewModel : ReactiveObject
{
    private readonly Action<string>? _onRomFileChanged;
    private bool _suppressRomFileChanged;
    private bool _isLoaded;
    private string _details;
    private string _foregroundColor;
    private string _romFile;

    public Apple2RomStatusViewModel(
        string name,
        bool isLoaded,
        bool isRequired,
        string details,
        string foregroundColor,
        string romFile,
        Action<string>? onRomFileChanged,
        bool isRunningInWebAssembly)
    {
        Name = name;
        _isLoaded = isLoaded;
        IsRequired = isRequired;
        _details = details;
        _foregroundColor = foregroundColor;
        _romFile = romFile;
        _onRomFileChanged = onRomFileChanged;
        IsRunningInWebAssembly = isRunningInWebAssembly;
    }

    public string Name { get; }
    public bool IsRequired { get; }
    public bool IsRunningInWebAssembly { get; }

    public bool IsLoaded
    {
        get => _isLoaded;
        set => this.RaiseAndSetIfChanged(ref _isLoaded, value);
    }

    public string Details
    {
        get => _details;
        set => this.RaiseAndSetIfChanged(ref _details, value);
    }

    public string ForegroundColor
    {
        get => _foregroundColor;
        set => this.RaiseAndSetIfChanged(ref _foregroundColor, value);
    }

    public string RomFile
    {
        get => _romFile;
        set => SetRomFile(value, suppressCallback: false);
    }

    public void UpdateFromData(Apple2RomStatusData data)
    {
        IsLoaded = data.IsLoaded;
        Details = data.Details;
        ForegroundColor = data.ForegroundColor;
        SetRomFile(data.RomFile, suppressCallback: true);
    }

    private void SetRomFile(string value, bool suppressCallback)
    {
        if (_romFile == value)
            return;

        this.RaiseAndSetIfChanged(ref _romFile, value);

        if (suppressCallback || _suppressRomFileChanged)
            return;

        try
        {
            _suppressRomFileChanged = true;
            _onRomFileChanged?.Invoke(value);
        }
        finally
        {
            _suppressRomFileChanged = false;
        }
    }
}

public record Apple2RomStatusData(
    string Name,
    bool IsLoaded,
    bool IsRequired,
    string Details,
    string ForegroundColor,
    string RomFile);

public record RenderProviderOption(Type Type, string DisplayName, string HelpText);

public record AudioProviderOption(Type Type, string DisplayName, string HelpText);

public record AudioTargetOption(Type Type, string DisplayName, string HelpText);

public record RenderTargetOption(Type Type, string DisplayName, string HelpText);

/// <summary>Selectable monitor type — a property of the screen, not the machine.</summary>
public record MonitorColorOption(Apple2MonitorColor MonitorColor, string DisplayName, string HelpText)
{
    public static readonly IReadOnlyList<MonitorColorOption> All = new[]
    {
        new MonitorColorOption(Apple2MonitorColor.Color, "Color",
            "Composite colour monitor: hi-res graphics show NTSC artifact colours."),
        new MonitorColorOption(Apple2MonitorColor.Green, "Green", "Classic green phosphor monitor."),
        new MonitorColorOption(Apple2MonitorColor.White, "White", "White phosphor / composite monochrome monitor."),
        new MonitorColorOption(Apple2MonitorColor.Amber, "Amber", "Amber phosphor monitor."),
    };

    public static MonitorColorOption FromMonitorColor(Apple2MonitorColor monitorColor)
        => All.FirstOrDefault(o => o.MonitorColor == monitorColor) ?? All[0];
}
