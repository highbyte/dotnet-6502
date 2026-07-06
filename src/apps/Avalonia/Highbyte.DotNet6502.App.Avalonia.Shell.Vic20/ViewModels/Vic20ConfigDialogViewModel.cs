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
using Highbyte.DotNet6502.Impl.Avalonia.Vic20;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Utils;
using Highbyte.DotNet6502.Utils;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;

public class Vic20ConfigDialogViewModel : ViewModelBase
{
    private const long MaxRomFileSizeBytes = 8 * 1024;

    private readonly AvaloniaHostApp _hostApp;
    private readonly Vic20HostConfig _originalConfig;
    private Vic20HostConfig _workingConfig;
    private readonly List<(Type renderProviderType, Type renderTargetType)> _renderCombinations;
    private readonly HttpClient _httpClient;
    private readonly ObservableCollection<string> _validationErrors = new();

    private bool _isBusy;
    private string? _statusMessage;
    private string? _validationMessage;
    private string _romDirectory = string.Empty;
    private RenderProviderOption? _selectedRenderProvider;
    private RenderTargetOption? _selectedRenderTarget;
    private bool _suppressRenderTargetUpdate;
    private CpuCompatibilityProfileOption? _selectedCpuCompatibilityProfile;

    public ReactiveCommand<Unit, Unit> DownloadRomsToByteArrayCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadRomsToFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearRomsCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public Vic20ConfigDialogViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _originalConfig = hostApp.CurrentHostSystemConfig as Vic20HostConfig
            ?? throw new Exception("hostApp.CurrentHostSystemConfig must be type Vic20HostConfig");
        _renderCombinations = hostApp.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations() ?? new List<(Type, Type)>();
        _workingConfig = (Vic20HostConfig)_originalConfig.Clone();
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
    public event EventHandler<Vic20RomLicenseAcknowledgementEventArgs>? RomLicenseAcknowledgementRequested;

    public ObservableCollection<Vic20RomStatusViewModel> RomStatuses { get; } = new();
    public ObservableCollection<RenderProviderOption> RenderProviders { get; } = new();
    public ObservableCollection<RenderTargetOption> RenderTargets { get; } = new();
    public ObservableCollection<CpuCompatibilityProfileOption> CpuCompatibilityProfiles { get; } =
        new(CpuCompatibilityProfileOption.All);

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
    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);
    public ObservableCollection<string> ValidationErrors => _validationErrors;
    public bool HasValidationErrors => _validationErrors.Count > 0;

    public string RomStatusSummary =>
        $"{RomStatuses.Count(r => r.IsRequired && r.IsLoaded)}/{Vic20SystemConfig.RequiredROMs.Count} ROMs loaded";

    public string RomDirectory
    {
        get => _romDirectory;
        set
        {
            if (_romDirectory == value)
                return;

            this.RaiseAndSetIfChanged(ref _romDirectory, value);
            _workingConfig.SystemConfig.ROMDirectory = value;
            UpdateValidationMessageFromConfig();
        }
    }

    public string RomDirectoryToolTip =>
        $"Optional ROM directory override. Leave blank to use the default: {PathHelper.ExpandOSEnvironmentVariables(Vic20SystemConfig.DefaultROMDirectory)}";

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

    public string OkButtonText => IsRunningInWebAssembly ? "Save" : "Ok";

    public Task AutoDownloadRomsToByteArrayAsync()
        => DownloadRomsToByteArrayAsync(requireAcknowledgement: true);

    public async Task<bool> DownloadRomsToByteArrayAsync(bool requireAcknowledgement)
    {
        if (requireAcknowledgement && !await RequestRomLicenseAcknowledgementAsync())
        {
            StatusMessage = "ROM download cancelled.";
            return false;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Downloading ROMs...";
            ValidationMessage = string.Empty;

            foreach (var romDownload in _workingConfig.SystemConfig.ROMDownloadUrls)
            {
                var proxyUrl = _hostApp.GetCorsProxyUrl();
                var fullROMUrl = !string.IsNullOrEmpty(proxyUrl)
                    ? $"{proxyUrl}{Uri.EscapeDataString(romDownload.Value)}"
                    : romDownload.Value;

                var romBytes = await _httpClient.GetByteArrayAsync(fullROMUrl);
                _workingConfig.SystemConfig.SetROM(romDownload.Key, data: romBytes);
            }

            StatusMessage = "ROMs downloaded successfully.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error downloading ROMs: {ex.Message}";
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
            StatusMessage = string.Empty;
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Downloading ROMs...";
            ValidationMessage = string.Empty;

            var romFolder = PathHelper.ExpandOSEnvironmentVariables(_workingConfig.SystemConfig.EffectiveROMDirectory);
            if (!Directory.Exists(romFolder))
                Directory.CreateDirectory(romFolder);

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");

            foreach (var romDownload in _workingConfig.SystemConfig.ROMDownloadUrls)
            {
                var romName = romDownload.Key;
                var romUrl = romDownload.Value;
                var filename = Path.GetFileName(new Uri(romUrl).LocalPath);
                var dest = Path.Combine(romFolder, filename);
                try
                {
                    using var response = await _httpClient.GetAsync(romUrl);
                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"Failed to get '{romUrl}' ({(int)response.StatusCode})");

                    await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                    _workingConfig.SystemConfig.SetROM(romName, filename);
                }
                catch (Exception ex)
                {
                    if (File.Exists(dest))
                        File.Delete(dest);
                    throw new Exception($"Error downloading {romUrl}: {ex.Message}", ex);
                }
            }

            StatusMessage = "ROMs downloaded successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error downloading ROMs: {ex.Message}";
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

                var romName = DetectRomName(fileName);
                if (romName == null)
                {
                    errors.Add($"Could not determine ROM type for file {fileName}. Expected names containing 'kern', 'bas', or 'char'.");
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
            StatusMessage = "ROM files loaded.";
            ValidationMessage = string.Empty;
        }
        else
        {
            StatusMessage = errors.Count < romData.Count ? "Some ROMs loaded with warnings." : null;
            ValidationMessage = string.Join(Environment.NewLine, errors);
        }

        return Task.CompletedTask;
    }

    public void UnloadRoms()
    {
        _workingConfig.SystemConfig.ROMs = new List<ROM>();
        UpdateRomStatuses();
        UpdateValidationMessageFromConfig();
        StatusMessage = "All ROMs cleared.";
    }

    public async Task<bool> TryApplyChangesAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Saving...";
            ValidationMessage = string.Empty;

            if (!_workingConfig.IsValid(out var validationErrors))
            {
                StatusMessage = null;
                ValidationMessage = string.Join(Environment.NewLine, validationErrors);
                return false;
            }

            _originalConfig.SystemConfig.ROMDirectory = _workingConfig.SystemConfig.ROMDirectory;
            _originalConfig.SystemConfig.ROMs = ROM.Clone(_workingConfig.SystemConfig.ROMs);
            _originalConfig.SystemConfig.CpuCompatibilityProfile = _workingConfig.SystemConfig.CpuCompatibilityProfile;

            if (_workingConfig.SystemConfig.RenderProviderType != null)
                _originalConfig.SystemConfig.SetRenderProviderType(_workingConfig.SystemConfig.RenderProviderType);

            if (_workingConfig.SystemConfig.RenderTargetType != null)
                _originalConfig.SystemConfig.SetRenderTargetType(_workingConfig.SystemConfig.RenderTargetType);

            _hostApp.UpdateHostSystemConfig(_originalConfig);
            await _hostApp.PersistCurrentHostSystemConfig();

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

    /// <summary>
    /// Populates all bound view-model properties from the current <see cref="_workingConfig"/>.
    /// Called from the constructor and after <see cref="ResetToDefaults"/> swaps the working config.
    /// </summary>
    private void LoadFromWorkingConfig()
    {
        RomDirectory = _workingConfig.SystemConfig.ROMDirectory;
        SelectedCpuCompatibilityProfile = CpuCompatibilityProfileOption.FromProfile(_workingConfig.SystemConfig.CpuCompatibilityProfile);

        InitializeRenderOptions();
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

        _workingConfig = new Vic20HostConfig();
        _workingConfig.SystemConfig.ROMs = preservedRoms;
        _workingConfig.SystemConfig.ROMDirectory = preservedRomDirectory;

        LoadFromWorkingConfig();

        StatusMessage = "Settings reset to defaults. Click Save to apply.";
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
        var desiredStatuses = new List<Vic20RomStatusData>();

        foreach (var required in Vic20SystemConfig.RequiredROMs)
        {
            var rom = roms.FirstOrDefault(r => string.Equals(r.Name, required, StringComparison.OrdinalIgnoreCase));
            desiredStatuses.Add(CreateRomStatusData(required, rom, isRequired: true));
        }

        foreach (var additional in roms.Where(r => !Vic20SystemConfig.RequiredROMs.Contains(r.Name)))
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
                RomStatuses.Insert(i, new Vic20RomStatusViewModel(
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

    private Vic20RomStatusData CreateRomStatusData(string romName, ROM? rom, bool isRequired)
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

        return new Vic20RomStatusData(romName, isLoaded, isRequired, details, foregroundColor, romFile);
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

    private static string? DetectRomName(string fileName)
    {
        if (fileName.Contains("kern", StringComparison.OrdinalIgnoreCase))
            return Vic20SystemConfig.KERNAL_ROM_NAME;
        if (fileName.Contains("bas", StringComparison.OrdinalIgnoreCase))
            return Vic20SystemConfig.BASIC_ROM_NAME;
        if (fileName.Contains("char", StringComparison.OrdinalIgnoreCase))
            return Vic20SystemConfig.CHARGEN_ROM_NAME;

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return Vic20SystemConfig.RequiredROMs.FirstOrDefault(required =>
            nameWithoutExtension.Contains(required, StringComparison.OrdinalIgnoreCase));
    }

    private Task<bool> RequestRomLicenseAcknowledgementAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        var args = new Vic20RomLicenseAcknowledgementEventArgs(tcs);
        RomLicenseAcknowledgementRequested?.Invoke(this, args);
        return tcs.Task;
    }
}

public class Vic20RomLicenseAcknowledgementEventArgs : EventArgs
{
    private readonly TaskCompletionSource<bool> _taskCompletionSource;

    public Vic20RomLicenseAcknowledgementEventArgs(TaskCompletionSource<bool> taskCompletionSource)
    {
        _taskCompletionSource = taskCompletionSource;
    }

    public void SetResult(bool acknowledged)
    {
        _taskCompletionSource.TrySetResult(acknowledged);
    }
}

public class Vic20RomStatusViewModel : ReactiveObject
{
    private readonly Action<string>? _onRomFileChanged;
    private bool _suppressRomFileChanged;
    private bool _isLoaded;
    private string _details;
    private string _foregroundColor;
    private string _romFile;

    public Vic20RomStatusViewModel(
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

    public void UpdateFromData(Vic20RomStatusData data)
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

public record Vic20RomStatusData(
    string Name,
    bool IsLoaded,
    bool IsRequired,
    string Details,
    string ForegroundColor,
    string RomFile);

public record RenderProviderOption(Type Type, string DisplayName, string HelpText);

public record RenderTargetOption(Type Type, string DisplayName, string HelpText);
