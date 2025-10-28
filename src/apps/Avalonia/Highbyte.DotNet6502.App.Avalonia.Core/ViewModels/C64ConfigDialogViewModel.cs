using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core.Config;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Utils;
using Highbyte.DotNet6502.Utils;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class C64ConfigDialogViewModel : ViewModelBase
{
    private const long MaxRomFileSizeBytes = 8 * 1024;

    private readonly AvaloniaHostApp _hostApp;
    private readonly C64HostConfig _originalConfig;
    private readonly C64HostConfig _workingConfig;
    private readonly List<(Type renderProviderType, Type renderTargetType)> _renderCombinations;
    private readonly HttpClient _httpClient;

    private bool _isBusy;
    private string? _statusMessage;
    private string? _validationMessage;
    private readonly ObservableCollection<string> _validationErrors = new();
    private bool _audioEnabled;
    private bool _keyboardJoystickEnabled;
    private int _selectedKeyboardJoystick;
    private int _selectedHostJoystick;
    private string _romDirectory = string.Empty;
    private RenderProviderOption? _selectedRenderProvider;
    private RenderTargetOption? _selectedRenderTarget;
    private bool _suppressRenderTargetUpdate;

    public C64ConfigDialogViewModel(
        AvaloniaHostApp hostApp,
        C64HostConfig originalConfig,
        List<(Type renderProviderType, Type renderTargetType)> renderCombinations)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _originalConfig = originalConfig ?? throw new ArgumentNullException(nameof(originalConfig));
        _renderCombinations = renderCombinations ?? new List<(Type, Type)>();
        _workingConfig = (C64HostConfig)originalConfig.Clone();
        _httpClient = new HttpClient();

        AvailableJoysticks = new ObservableCollection<int>(_workingConfig.InputConfig.AvailableJoysticks);
        AudioEnabled = _workingConfig.SystemConfig.AudioEnabled;
        KeyboardJoystickEnabled = _workingConfig.SystemConfig.KeyboardJoystickEnabled;
        SelectedKeyboardJoystick = _workingConfig.SystemConfig.KeyboardJoystick;
        SelectedHostJoystick = _workingConfig.InputConfig.CurrentJoystick;
        RomDirectory = _workingConfig.SystemConfig.ROMDirectory;

        InitializeRenderOptions();
        UpdateRomStatuses();
        UpdateKeyboardMappings();
        UpdateValidationMessageFromConfig();
    }

    public ObservableCollection<RomStatusViewModel> RomStatuses { get; } = new();
    public ObservableCollection<RenderProviderOption> RenderProviders { get; } = new();
    public ObservableCollection<RenderTargetOption> RenderTargets { get; } = new();
    public ObservableCollection<int> AvailableJoysticks { get; }
    public ObservableCollection<KeyMappingEntry> KeyboardMappings { get; } = new();

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
        $"{RomStatuses.Count(r => r.IsRequired && r.IsLoaded)}/{C64SystemConfig.RequiredROMs.Count} ROMs loaded";

    public bool AudioEnabled
    {
        get => _audioEnabled;
        set
        {
            if (_audioEnabled == value)
                return;

            this.RaiseAndSetIfChanged(ref _audioEnabled, value);
            _workingConfig.SystemConfig.AudioEnabled = value;
        }
    }

    public bool KeyboardJoystickEnabled
    {
        get => _keyboardJoystickEnabled;
        set
        {
            if (_keyboardJoystickEnabled == value)
                return;

            this.RaiseAndSetIfChanged(ref _keyboardJoystickEnabled, value);
            _workingConfig.SystemConfig.KeyboardJoystickEnabled = value;
        }
    }

    public int SelectedKeyboardJoystick
    {
        get => _selectedKeyboardJoystick;
        set
        {
            if (_selectedKeyboardJoystick == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedKeyboardJoystick, value);
            _workingConfig.SystemConfig.KeyboardJoystick = value;
            UpdateValidationMessageFromConfig();
        }
    }

    public int SelectedHostJoystick
    {
        get => _selectedHostJoystick;
        set
        {
            if (_selectedHostJoystick == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedHostJoystick, value);
            _workingConfig.InputConfig.CurrentJoystick = value;
            UpdateKeyboardMappings();
        }
    }

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
            {
                _workingConfig.SystemConfig.SetRenderTargetType(value.Type);
            }

            this.RaisePropertyChanged(nameof(SelectedRenderTargetHelpText));
        }
    }

    public string SelectedRenderProviderHelpText => SelectedRenderProvider?.HelpText ?? string.Empty;

    public string SelectedRenderTargetHelpText => SelectedRenderTarget?.HelpText ?? string.Empty;

    public string OkButtonText => IsRunningInWebAssembly ? "Save" : "Ok";

    public async Task AutoDownloadRomsToByteArrayAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Downloading ROMs...";
            ValidationMessage = string.Empty;

            foreach (var romDownload in _workingConfig.SystemConfig.ROMDownloadUrls)
            {
                var fullROMUrl = !string.IsNullOrEmpty(_workingConfig.CorsProxyURL)
                    ? $"{_workingConfig.CorsProxyURL}{Uri.EscapeDataString(romDownload.Value)}"
                    : romDownload.Value;

                var romBytes = await _httpClient.GetByteArrayAsync(fullROMUrl);
                _workingConfig.SystemConfig.SetROM(romDownload.Key, data: romBytes);
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

    public async Task AutoDownloadROMsToFilesAsync()
    {

        try
        {
            IsBusy = true;
            StatusMessage = "Downloading ROMs...";
            ValidationMessage = string.Empty;

            var romFolder = PathHelper.ExpandOSEnvironmentVariables(_workingConfig.SystemConfig.ROMDirectory);
            if (!Directory.Exists(romFolder))
            {
                Directory.CreateDirectory(romFolder);
            }

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            //_httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
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
                    System.Console.WriteLine($"Downloaded {filename} to {dest}");

                    // Update the C64SystemConfig with the downloaded ROM file
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
        catch (System.Exception ex)
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

        var errors = new List<string>();
        foreach (var (fileName, data) in romDataList)
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
            StatusMessage = errors.Count < romDataList.Count() ? "Some ROMs loaded with warnings." : null;
            ValidationMessage = string.Join(Environment.NewLine, errors);
        }

        return Task.CompletedTask;
    }

    public async Task LoadRomsFromFilesAsync(IEnumerable<string> filePaths)
    {
        if (filePaths == null)
            return;

        var errors = new List<string>();
        foreach (var filePath in filePaths)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    errors.Add($"File not found: {fileInfo.Name}");
                    continue;
                }

                if (fileInfo.Length > MaxRomFileSizeBytes)
                {
                    errors.Add($"File {fileInfo.Name} is larger than {MaxRomFileSizeBytes} bytes.");
                    continue;
                }

                var romName = DetectRomName(fileInfo.Name);
                if (romName == null)
                {
                    errors.Add($"Could not determine ROM type for file {fileInfo.Name}. Expected names containing 'kern', 'bas', or 'char'.");
                    continue;
                }

                var data = await File.ReadAllBytesAsync(fileInfo.FullName);
                _workingConfig.SystemConfig.SetROM(romName, data: data);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to load {Path.GetFileName(filePath)}: {ex.Message}");
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
            StatusMessage = errors.Count < filePaths.Count() ? "Some ROMs loaded with warnings." : null;
            ValidationMessage = string.Join(Environment.NewLine, errors);
        }
    }

    public void UnloadRoms()
    {
        _workingConfig.SystemConfig.ROMs = new List<ROM>();
        UpdateRomStatuses();
        UpdateValidationMessageFromConfig();
        StatusMessage = "All ROMs cleared.";
    }

    public async Task<bool> TryApplyChanges()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Saving...";
            ValidationMessage = string.Empty;

            if (!_workingConfig.IsValid(out var validationErrors))
            {
                StatusMessage = null;
                ValidationMessage = string.Join(Environment.NewLine, validationErrors); ;
                return false;
            }

            ApplyWorkingConfigToOriginal();

            _hostApp.UpdateHostSystemConfig(_originalConfig);
            await _hostApp.PersistCurrentHostSystemConfig();

            StatusMessage = "Configuration saved.";
            return true;

        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving config: {ex.Message}"; ;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void InitializeRenderOptions()
    {
        RenderProviders.Clear();

        var providerTypes = _renderCombinations.Select(c => c.renderProviderType).Distinct().ToList();
        if (_workingConfig.SystemConfig.RenderProviderType != null && !providerTypes.Contains(_workingConfig.SystemConfig.RenderProviderType))
        {
            providerTypes.Add(_workingConfig.SystemConfig.RenderProviderType);
        }

        foreach (var providerType in providerTypes)
        {
            RenderProviders.Add(new RenderProviderOption(
                providerType,
                TypeDisplayHelper.GetDisplayName(providerType),
                TypeDisplayHelper.GetHelpText(providerType)));
        }

        SelectedRenderProvider = RenderProviders.FirstOrDefault(rp => rp.Type == _workingConfig.SystemConfig.RenderProviderType)
            ?? RenderProviders.FirstOrDefault();

        if (SelectedRenderProvider != null && _workingConfig.SystemConfig.RenderProviderType == null)
        {
            _workingConfig.SystemConfig.SetRenderProviderType(SelectedRenderProvider.Type);
        }

        if (SelectedRenderProvider != null)
        {
            UpdateRenderTargetsForProvider(SelectedRenderProvider.Type);
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

            if (_workingConfig.SystemConfig.RenderTargetType != null && !targetTypes.Contains(_workingConfig.SystemConfig.RenderTargetType))
            {
                targetTypes.Add(_workingConfig.SystemConfig.RenderTargetType);
            }

            foreach (var targetType in targetTypes)
            {
                RenderTargets.Add(new RenderTargetOption(
                    targetType,
                    TypeDisplayHelper.GetDisplayName(targetType),
                    TypeDisplayHelper.GetHelpText(targetType)));
            }

            SelectedRenderTarget = RenderTargets.FirstOrDefault(rt => rt.Type == _workingConfig.SystemConfig.RenderTargetType)
                ?? RenderTargets.FirstOrDefault();

            if (SelectedRenderTarget != null && _workingConfig.SystemConfig.RenderTargetType == null)
            {
                _workingConfig.SystemConfig.SetRenderTargetType(SelectedRenderTarget.Type);
            }
        }
        finally
        {
            _suppressRenderTargetUpdate = false;
        }
    }

    private void UpdateRomStatuses()
    {
        var roms = _workingConfig.SystemConfig.ROMs;
        var desiredStatuses = new List<RomStatusData>();

        foreach (var required in C64SystemConfig.RequiredROMs)
        {
            var rom = roms.FirstOrDefault(r => string.Equals(r.Name, required, StringComparison.OrdinalIgnoreCase));
            desiredStatuses.Add(CreateRomStatusData(required, rom, isRequired: true));
        }

        foreach (var additional in roms.Where(r => !C64SystemConfig.RequiredROMs.Contains(r.Name)))
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
                {
                    RomStatuses.Move(currentIndex, i);
                }

                existingByName.Remove(statusData.Name);
            }
            else
            {
                var romName = statusData.Name;
                var newStatus = new RomStatusViewModel(
                    romName,
                    statusData.IsLoaded,
                    statusData.IsRequired,
                    statusData.Details,
                    statusData.ForegroundColor,
                    statusData.RomFile,
                    filePath => UpdateRomFile(romName, filePath),
                    IsRunningInWebAssembly);

                RomStatuses.Insert(i, newStatus);
            }
        }

        foreach (var obsolete in existingByName.Values)
        {
            RomStatuses.Remove(obsolete);
        }

        this.RaisePropertyChanged(nameof(RomStatusSummary));
    }

    private RomStatusData CreateRomStatusData(string romName, ROM? rom, bool isRequired)
    {
        var romFile = rom?.File ?? string.Empty;
        var romDataLength = rom?.Data?.Length ?? 0;
        var hasData = romDataLength > 0;
        var hasFile = !string.IsNullOrWhiteSpace(romFile);

        bool fileExists = false;
        if (hasFile && rom != null)
        {
            try
            {
                var romFilePath = rom.GetROMFilePath(_workingConfig.SystemConfig.ROMDirectory);
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

        return new RomStatusData(
            romName,
            isLoaded,
            isRequired,
            details,
            foregroundColor,
            romFile);
    }

    private void UpdateRomFile(string romName, string romFile)
    {
        var trimmedFile = string.IsNullOrWhiteSpace(romFile) ? null : romFile.Trim();

        ROM? existingRom = null;
        if (_workingConfig.SystemConfig.HasROM(romName))
        {
            existingRom = _workingConfig.SystemConfig.GetROM(romName);
        }

        if (trimmedFile != null)
        {
            _workingConfig.SystemConfig.SetROM(romName, file: trimmedFile, data: null);
        }
        else
        {
            _workingConfig.SystemConfig.SetROM(romName, file: null, data: existingRom?.Data);
        }

        UpdateRomStatuses();
        UpdateValidationMessageFromConfig();
    }

    private void UpdateKeyboardMappings()
    {
        KeyboardMappings.Clear();

        if (_workingConfig.InputConfig.KeyToC64JoystickMap.TryGetValue(SelectedHostJoystick, out var map))
        {
            foreach (var kvp in map)
            {
                KeyboardMappings.Add(new KeyMappingEntry(kvp.Key.ToString(), kvp.Value.ToString()));
            }
        }
    }

    private void UpdateValidationMessageFromConfig()
    {
        if (!_workingConfig.IsValid(out var validationErrors))
        {
            ValidationMessage = string.Join(Environment.NewLine, validationErrors);
            
            // Update ValidationErrors collection
            _validationErrors.Clear();
            foreach (var error in validationErrors)
            {
                _validationErrors.Add(error);
            }
        }
        else
        {
            ValidationMessage = string.Empty;
            _validationErrors.Clear();
        }
        
        // Notify UI about changes
        this.RaisePropertyChanged(nameof(HasValidationErrors));
        this.RaisePropertyChanged(nameof(CanSave));
    }

    private void ApplyWorkingConfigToOriginal()
    {
        _originalConfig.SystemConfig.ROMDirectory = _workingConfig.SystemConfig.ROMDirectory;
        _originalConfig.SystemConfig.AudioEnabled = _workingConfig.SystemConfig.AudioEnabled;
        _originalConfig.SystemConfig.KeyboardJoystickEnabled = _workingConfig.SystemConfig.KeyboardJoystickEnabled;
        _originalConfig.SystemConfig.KeyboardJoystick = _workingConfig.SystemConfig.KeyboardJoystick;
        _originalConfig.SystemConfig.ColorMapName = _workingConfig.SystemConfig.ColorMapName;

        if (_workingConfig.SystemConfig.RenderProviderType != null)
            _originalConfig.SystemConfig.SetRenderProviderType(_workingConfig.SystemConfig.RenderProviderType);

        if (_workingConfig.SystemConfig.RenderTargetType != null)
            _originalConfig.SystemConfig.SetRenderTargetType(_workingConfig.SystemConfig.RenderTargetType);

        _originalConfig.SystemConfig.ROMs = ROM.Clone(_workingConfig.SystemConfig.ROMs);
        _originalConfig.InputConfig = (C64AvaloniaInputConfig)_workingConfig.InputConfig.Clone();
    }

    private static string? DetectRomName(string fileName)
    {
        if (fileName.Contains("kern", StringComparison.OrdinalIgnoreCase))
            return C64SystemConfig.KERNAL_ROM_NAME;
        if (fileName.Contains("bas", StringComparison.OrdinalIgnoreCase))
            return C64SystemConfig.BASIC_ROM_NAME;
        if (fileName.Contains("char", StringComparison.OrdinalIgnoreCase))
            return C64SystemConfig.CHARGEN_ROM_NAME;

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return C64SystemConfig.RequiredROMs.FirstOrDefault(required =>
            nameWithoutExtension.Contains(required, StringComparison.OrdinalIgnoreCase));
    }
}

public class RomStatusViewModel : ReactiveObject
{
    private readonly Action<string>? _onRomFileChanged;
    private bool _suppressRomFileChanged;

    private bool _isLoaded;
    private string _details;
    private string _foregroundColor;
    private string _romFile;

    public RomStatusViewModel(
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
    public bool IsRunningInWebAssembly { get; }

    internal void UpdateFromData(RomStatusData data)
    {
        IsLoaded = data.IsLoaded;
        Details = data.Details;
        ForegroundColor = data.ForegroundColor;
        SetRomFile(data.RomFile, suppressCallback: true);
    }

    private void SetRomFile(string value, bool suppressCallback)
    {
        if (EqualityComparer<string>.Default.Equals(_romFile, value))
            return;

        var shouldRestore = _suppressRomFileChanged;
        _suppressRomFileChanged = suppressCallback;
        try
        {
            this.RaiseAndSetIfChanged(ref _romFile, value);
        }
        finally
        {
            _suppressRomFileChanged = shouldRestore;
        }

        if (!suppressCallback)
            _onRomFileChanged?.Invoke(value);
    }
}

public record RenderProviderOption(Type Type, string DisplayName, string HelpText);

public record RenderTargetOption(Type Type, string DisplayName, string HelpText);

public record KeyMappingEntry(string Key, string Action);

internal readonly record struct RomStatusData(
    string Name,
    bool IsLoaded,
    bool IsRequired,
    string Details,
    string ForegroundColor,
    string RomFile);
