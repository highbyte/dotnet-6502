using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading.Tasks;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.AI.CodingAssistant.Inference.OpenAI;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Highbyte.DotNet6502.Systems.Utils;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using static Highbyte.DotNet6502.AI.CodingAssistant.CustomAIEndpointCodeSuggestion;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.ViewModels;

public class C64ConfigDialogViewModel : ViewModelBase
{
    private const long MaxRomFileSizeBytes = 8 * 1024;

    // Keyboard layout dropdown entry meaning "no explicit setting" -> null config -> auto-detect.
    private const string AutoKeyboardLayoutLabel = "Auto";
    private const string DefaultSwiftLinkBridgeTargetLabel = "Worker default";

    private readonly AvaloniaHostApp _hostApp;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly C64HostConfig _originalConfig;
    private C64HostConfig _workingConfig;
    private readonly List<(Type renderProviderType, Type renderTargetType)> _renderCombinations;
    private readonly List<(Type audioProviderType, Type audioTargetType)> _audioCombinations;
    private readonly HttpClient _httpClient;

    private bool _isBusy;
    private string? _statusMessage;
    private bool _statusMessageIsError;
    private string? _validationMessage;
    private readonly ObservableCollection<string> _validationErrors = new();
    private bool _audioEnabled;
    private bool _vic2RasterizerPerLineSprites;
    private bool _basicAIAssistantEnabled;
    private bool _keyboardJoystickEnabled;
    private int _selectedKeyboardJoystick;
    private int _selectedHostJoystick;
    private string _selectedKeyboardLayout = AutoKeyboardLayoutLabel;
    private bool _swiftLinkEnabled;
    private C64CartridgeIOAddress _selectedSwiftLinkCartridgeIOAddress;
    private C64SwiftLinkTransportMode _selectedSwiftLinkTransportMode;
    private C64SwiftLinkInterruptMode _selectedSwiftLinkInterruptMode;
    private C64SwiftLinkReceiveMode _selectedSwiftLinkReceiveMode;
    private string _swiftLinkTcpHost = string.Empty;
    private int _swiftLinkTcpPort;
    private string _swiftLinkTcpPortText = string.Empty;
    private bool _swiftLinkConnectOnBoot;
    private string _swiftLinkBridgeWebSocketUrl = string.Empty;
    private string _swiftLinkSharedToken = string.Empty;
    private string _swiftLinkBridgeTargetId = string.Empty;
    private SwiftLinkBridgeTargetOption? _selectedSwiftLinkBridgeTarget;
    private string _romDirectory = string.Empty;
    private RenderProviderOption? _selectedRenderProvider;
    private RenderTargetOption? _selectedRenderTarget;
    private bool _suppressRenderTargetUpdate;
    private AudioProviderOption? _selectedAudioProvider;
    private AudioTargetOption? _selectedAudioTarget;
    private bool _suppressAudioTargetUpdate;
    private CpuCompatibilityProfileOption? _selectedCpuCompatibilityProfile;
    private SidEmulationModeOption? _selectedSidEmulationMode;

    // AI Coding Assistant properties - now using config objects
    private CodeSuggestionBackendTypeEnum _selectedAIBackendType;
    private ApiConfig _openAIConfig = new();
    private ApiConfig _openAISelfHostedConfig = new();
    private CustomAIEndpointConfig _customEndpointConfig = new();
    private string _aiTestStatusMessage = string.Empty;
    private bool _aiTestStatusIsSuccess = false;

    // ReactiveUI Commands
    public ReactiveCommand<Unit, Unit> DownloadRomsToByteArrayCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadRomsToFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearRomsCommand { get; }
    public ReactiveCommand<Unit, Unit> TestAIBackendCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public C64ConfigDialogViewModel(
        AvaloniaHostApp hostApp,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(nameof(C64ConfigDialogViewModel));
        _originalConfig = hostApp.CurrentHostSystemConfig as C64HostConfig ?? throw new Exception("hostApp.CurrentHostSystemConfig must be type C64HostConfig");
        _renderCombinations = hostApp.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations() ?? new List<(Type, Type)>();
        _audioCombinations = hostApp.GetAvailableSystemAudioProviderTypesAndAudioTargetTypeCombinations() ?? new List<(Type, Type)>();
        _workingConfig = (C64HostConfig)_originalConfig.Clone();
        _httpClient = new HttpClient();

        foreach (var transportMode in Enum.GetValues<C64SwiftLinkTransportMode>())
        {
            AvailableSwiftLinkTransportModes.Add(transportMode);
        }

        AvailableJoysticks = new ObservableCollection<int>(_workingConfig.InputConfig.AvailableJoysticks);

        LoadFromWorkingConfig();

        // Initialize ReactiveUI Commands with MainThreadScheduler for Browser compatibility
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

        TestAIBackendCommand = ReactiveCommandHelper.CreateSafeCommand(
            TestAIBackendAsync,
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
    }

    public event EventHandler<bool>? ConfigurationChanged;

    public ObservableCollection<RomStatusViewModel> RomStatuses { get; } = new();
    public ObservableCollection<RenderProviderOption> RenderProviders { get; } = new();
    public ObservableCollection<RenderTargetOption> RenderTargets { get; } = new();
    public ObservableCollection<AudioProviderOption> AudioProviders { get; } = new();
    public ObservableCollection<AudioTargetOption> AudioTargets { get; } = new();
    public ObservableCollection<CpuCompatibilityProfileOption> CpuCompatibilityProfiles { get; } =
        new(CpuCompatibilityProfileOption.All);
    public ObservableCollection<SidEmulationModeOption> SidEmulationModes { get; } = new();
    public ObservableCollection<int> AvailableJoysticks { get; }
    public ObservableCollection<KeyMappingEntry> KeyboardMappings { get; } = new();
    public ObservableCollection<C64CartridgeIOAddress> AvailableSwiftLinkCartridgeIOAddresses { get; } =
        new(Enum.GetValues<C64CartridgeIOAddress>());
    public ObservableCollection<C64SwiftLinkTransportMode> AvailableSwiftLinkTransportModes { get; } = new();
    public ObservableCollection<C64SwiftLinkInterruptMode> AvailableSwiftLinkInterruptModes { get; } =
        new(Enum.GetValues<C64SwiftLinkInterruptMode>());
    public ObservableCollection<C64SwiftLinkReceiveMode> AvailableSwiftLinkReceiveModes { get; } =
        new(Enum.GetValues<C64SwiftLinkReceiveMode>());
    public ObservableCollection<SwiftLinkBridgeTargetOption> AvailableSwiftLinkBridgeTargets { get; } = new();
    // "Auto" (auto-detect) plus each explicit C64KeyboardLayout, as strings for the dropdown.
    public ObservableCollection<string> AvailableKeyboardLayouts { get; } =
        new(new[] { AutoKeyboardLayoutLabel }.Concat(Enum.GetNames<C64KeyboardLayout>()));

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
        $"{RomStatuses.Count(r => r.IsRequired && r.IsLoaded)}/{C64SystemConfig.RequiredROMs.Count} ROMs loaded";

    public bool SwiftLinkEnabled
    {
        get => _swiftLinkEnabled;
        set
        {
            if (_swiftLinkEnabled == value)
                return;

            this.RaiseAndSetIfChanged(ref _swiftLinkEnabled, value);
            _workingConfig.SystemConfig.SwiftLink.Enabled = value;
            UpdateValidationMessageFromConfig();
        }
    }

    public C64CartridgeIOAddress SelectedSwiftLinkCartridgeIOAddress
    {
        get => _selectedSwiftLinkCartridgeIOAddress;
        set
        {
            if (_selectedSwiftLinkCartridgeIOAddress == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedSwiftLinkCartridgeIOAddress, value);
            _workingConfig.SystemConfig.SwiftLink.CartridgeIOAddress = value;
            UpdateValidationMessageFromConfig();
        }
    }

    public C64SwiftLinkInterruptMode SelectedSwiftLinkInterruptMode
    {
        get => _selectedSwiftLinkInterruptMode;
        set
        {
            if (_selectedSwiftLinkInterruptMode == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedSwiftLinkInterruptMode, value);
            _workingConfig.SystemConfig.SwiftLink.InterruptMode = value;
            UpdateValidationMessageFromConfig();
        }
    }

    public C64SwiftLinkReceiveMode SelectedSwiftLinkReceiveMode
    {
        get => _selectedSwiftLinkReceiveMode;
        set
        {
            if (_selectedSwiftLinkReceiveMode == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedSwiftLinkReceiveMode, value);
            _workingConfig.SystemConfig.SwiftLink.ReceiveMode = value;
            UpdateValidationMessageFromConfig();
        }
    }

    public C64SwiftLinkTransportMode SelectedSwiftLinkTransportMode
    {
        get => _selectedSwiftLinkTransportMode;
        set
        {
            if (_selectedSwiftLinkTransportMode == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedSwiftLinkTransportMode, value);
            _workingConfig.SwiftLinkHost.TransportMode = value;
            if (value != C64SwiftLinkTransportMode.RawTcp && _swiftLinkConnectOnBoot)
            {
                this.RaiseAndSetIfChanged(ref _swiftLinkConnectOnBoot, false, nameof(SwiftLinkConnectOnBoot));
                _workingConfig.SwiftLinkHost.ConnectOnBoot = false;
            }
            this.RaisePropertyChanged(nameof(IsSwiftLinkConnectOnBootAvailable));
            this.RaisePropertyChanged(nameof(SwiftLinkConnectOnBootToolTip));
            UpdateValidationMessageFromConfig();
        }
    }

    public bool IsSwiftLinkConnectOnBootAvailable
        => SelectedSwiftLinkTransportMode == C64SwiftLinkTransportMode.RawTcp;

    public string SwiftLinkConnectOnBootToolTip
        => IsSwiftLinkConnectOnBootAvailable
            ? IsRunningInWebAssembly
                ? "Automatically opens the configured WebSocket bridge when the emulator starts."
                : "Automatically opens the configured raw TCP SwiftLink connection when the emulator starts."
            : IsRunningInWebAssembly
                ? "Hayes modem mode waits for the emulated C64 software to dial with an ATDT command, then connects through the fixed-target WebSocket bridge."
                : "Hayes modem mode waits for the emulated C64 software to dial with an ATDT command.";

    public string SwiftLinkTcpHost
    {
        get => _swiftLinkTcpHost;
        set
        {
            if (_swiftLinkTcpHost == value)
                return;

            this.RaiseAndSetIfChanged(ref _swiftLinkTcpHost, value);
            _workingConfig.SwiftLinkHost.TcpHost = value;
            UpdateValidationMessageFromConfig();
        }
    }

    public int SwiftLinkTcpPort
    {
        get => _swiftLinkTcpPort;
        set
        {
            if (_swiftLinkTcpPort == value)
                return;

            this.RaiseAndSetIfChanged(ref _swiftLinkTcpPort, value);
            this.RaiseAndSetIfChanged(ref _swiftLinkTcpPortText, value.ToString(CultureInfo.InvariantCulture), nameof(SwiftLinkTcpPortText));
            _workingConfig.SwiftLinkHost.TcpPort = value;
            UpdateValidationMessageFromConfig();
        }
    }

    public string SwiftLinkTcpPortText
    {
        get => _swiftLinkTcpPortText;
        set
        {
            if (_swiftLinkTcpPortText == value)
                return;

            this.RaiseAndSetIfChanged(ref _swiftLinkTcpPortText, value);

            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort))
            {
                if (_swiftLinkTcpPort != parsedPort)
                {
                    this.RaiseAndSetIfChanged(ref _swiftLinkTcpPort, parsedPort, nameof(SwiftLinkTcpPort));
                    _workingConfig.SwiftLinkHost.TcpPort = parsedPort;
                }
            }

            UpdateValidationMessageFromConfig();
        }
    }

    public bool SwiftLinkConnectOnBoot
    {
        get => _swiftLinkConnectOnBoot;
        set
        {
            if (_swiftLinkConnectOnBoot == value)
                return;

            this.RaiseAndSetIfChanged(ref _swiftLinkConnectOnBoot, value);
            _workingConfig.SwiftLinkHost.ConnectOnBoot = value;
            UpdateValidationMessageFromConfig();
        }
    }

    public string SwiftLinkBridgeWebSocketUrl
    {
        get => _swiftLinkBridgeWebSocketUrl;
        set
        {
            if (_swiftLinkBridgeWebSocketUrl == value)
                return;

            this.RaiseAndSetIfChanged(ref _swiftLinkBridgeWebSocketUrl, value);
            _workingConfig.SwiftLinkWebSocketBridgeUrl = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            UpdateValidationMessageFromConfig();
        }
    }

    public string SwiftLinkSharedToken
    {
        get => _swiftLinkSharedToken;
        set
        {
            if (_swiftLinkSharedToken == value)
                return;

            this.RaiseAndSetIfChanged(ref _swiftLinkSharedToken, value);
            _workingConfig.SwiftLinkSharedToken = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            UpdateValidationMessageFromConfig();
        }
    }

    public string SwiftLinkBridgeTargetId
    {
        get => _swiftLinkBridgeTargetId;
        set
        {
            if (_swiftLinkBridgeTargetId == value)
                return;

            this.RaiseAndSetIfChanged(ref _swiftLinkBridgeTargetId, value);
            _workingConfig.SwiftLinkBridgeTargetId = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            UpdateValidationMessageFromConfig();
        }
    }

    public SwiftLinkBridgeTargetOption? SelectedSwiftLinkBridgeTarget
    {
        get => _selectedSwiftLinkBridgeTarget;
        set
        {
            if (_selectedSwiftLinkBridgeTarget == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedSwiftLinkBridgeTarget, value);
            SwiftLinkBridgeTargetId = value?.Value ?? string.Empty;
        }
    }

    public static string SwiftLinkBridgeWebSocketUrlWatermark => C64HostConfig.DefaultSwiftLinkWebSocketBridgeUrl;

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

    /// <summary>
    /// When enabled, the Rasterizer render provider draws sprites per raster line (enabling sprite
    /// multiplexing) and accumulates sprite collision per raster line, instead of once at end-of-frame.
    /// Only affects the Rasterizer (Vic2Rasterizer) render provider.
    /// </summary>
    public bool Vic2RasterizerPerLineSprites
    {
        get => _vic2RasterizerPerLineSprites;
        set
        {
            if (_vic2RasterizerPerLineSprites == value)
                return;

            this.RaiseAndSetIfChanged(ref _vic2RasterizerPerLineSprites, value);
            _workingConfig.SystemConfig.Vic2RasterizerPerLineSprites = value;
        }
    }

    /// <summary>
    /// Whether the BASIC AI coding assistant is enabled by default when the C64 starts. The F9 key
    /// still toggles it live while running. (Moved here from the C64 menu to keep it with the other
    /// AI assistant settings.)
    /// </summary>
    public bool BasicAIAssistantEnabled
    {
        get => _basicAIAssistantEnabled;
        set
        {
            if (_basicAIAssistantEnabled == value)
                return;

            this.RaiseAndSetIfChanged(ref _basicAIAssistantEnabled, value);
            _workingConfig.BasicAIAssistantDefaultEnabled = value;
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

    public string SelectedKeyboardLayout
    {
        get => _selectedKeyboardLayout;
        set
        {
            if (_selectedKeyboardLayout == value)
                return;

            this.RaiseAndSetIfChanged(ref _selectedKeyboardLayout, value);
            // "Auto" (or empty) -> null config -> auto-detect; otherwise a forced layout.
            _workingConfig.InputConfig.KeyboardLayout =
                string.IsNullOrEmpty(value) || value == AutoKeyboardLayoutLabel
                    ? null
                    : Enum.Parse<C64KeyboardLayout>(value);
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

    public string RomDirectoryToolTip =>
        $"Optional ROM folder override. Leave blank to use the default: {PathHelper.ExpandOSEnvironmentVariables(C64SystemConfig.DefaultROMDirectory)}";

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

    public AudioTargetOption? SelectedAudioTarget
    {
        get => _selectedAudioTarget;
        set
        {
            if (ReferenceEquals(_selectedAudioTarget, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedAudioTarget, value);

            if (value != null && !_suppressAudioTargetUpdate)
            {
                _workingConfig.SystemConfig.SetAudioTargetType(value.Type);
            }

            this.RaisePropertyChanged(nameof(SelectedAudioTargetHelpText));
        }
    }

    public string SelectedAudioProviderHelpText => SelectedAudioProvider?.HelpText ?? string.Empty;

    public string SelectedAudioTargetHelpText => SelectedAudioTarget?.HelpText ?? string.Empty;

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

    public SidEmulationModeOption? SelectedSidEmulationMode
    {
        get => _selectedSidEmulationMode;
        set
        {
            if (ReferenceEquals(_selectedSidEmulationMode, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedSidEmulationMode, value);

            if (value != null)
                _workingConfig.SystemConfig.SidEmulationMode = value.Mode;

            this.RaisePropertyChanged(nameof(SelectedSidEmulationModeHelpText));
        }
    }

    public string SelectedSidEmulationModeHelpText => SelectedSidEmulationMode?.HelpText ?? string.Empty;

    public string OkButtonText => IsRunningInWebAssembly ? "Save" : "Ok";

    public Task AutoDownloadRomsToByteArrayAsync()
        => DownloadRomsToByteArrayAsync(requireAcknowledgement: true);

    private void SetStatusMessage(string? message, bool isError = false)
    {
        StatusMessage = message;
        StatusMessageIsError = !string.IsNullOrEmpty(message) && isError;
    }

    public async Task<bool> DownloadRomsToByteArrayAsync(bool requireAcknowledgement)
    {
        if (requireAcknowledgement && !await RequestRomLicenseAcknowledgementAsync())
        {
            _logger.LogInformation("C64 ROM download to memory cancelled by user.");
            SetStatusMessage("ROM download cancelled.");
            return false;
        }

        try
        {
            _logger.LogInformation("Starting C64 ROM download to in-memory byte arrays.");
            IsBusy = true;
            SetStatusMessage("Downloading ROMs...");
            ValidationMessage = string.Empty;

            foreach (var romDownload in _workingConfig.SystemConfig.ROMDownloadUrls)
            {
                var romName = romDownload.Key;
                var romUrl = romDownload.Value;
                var proxyUrl = _hostApp.GetCorsProxyUrl();
                var fullROMUrl = !string.IsNullOrEmpty(proxyUrl)
                    ? $"{proxyUrl}{Uri.EscapeDataString(romUrl)}"
                    : romUrl;

                try
                {
                    _logger.LogInformation(
                        "Downloading ROM {RomName} from {SourceUrl} using request URL {RequestUrl}",
                        romName,
                        romUrl,
                        fullROMUrl);

                    var romBytes = await _httpClient.GetByteArrayAsync(fullROMUrl);
                    _workingConfig.SystemConfig.SetROM(romName, data: romBytes);
                    _logger.LogInformation("Downloaded ROM {RomName}: {ByteCount} bytes", romName, romBytes.Length);
                }
                catch (Exception ex)
                {
                    var userMessage = DownloadErrorHelper.BuildDownloadFailureMessage(
                        $"ROM '{romName}'",
                        romUrl,
                        fullROMUrl,
                        ex);

                    _logger.LogError(
                        ex,
                        "Failed to download ROM {RomName}. Source URL: {SourceUrl}. Request URL: {RequestUrl}. Details: {ErrorSummary}",
                        romName,
                        romUrl,
                        fullROMUrl,
                        DownloadErrorHelper.FlattenExceptionMessages(ex));

                    throw new InvalidOperationException(userMessage, ex);
                }
            }

            SetStatusMessage("ROMs downloaded successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "C64 ROM download to memory failed. Details: {ErrorSummary}",
                DownloadErrorHelper.FlattenExceptionMessages(ex));
            SetStatusMessage(ex.Message, isError: true);
            return false;
        }
        finally
        {
            IsBusy = false;
            RomStatuses.Clear();
            UpdateRomStatuses();
            UpdateValidationMessageFromConfig();
        }
    }

    public async Task AutoDownloadROMsToFilesAsync()
    {
        // Request acknowledgement before downloading
        if (!await RequestRomLicenseAcknowledgementAsync())
        {
            _logger.LogInformation("C64 ROM download to files cancelled by user.");
            SetStatusMessage(string.Empty);
            return;
        }

        try
        {
            _logger.LogInformation("Starting C64 ROM download to files.");
            IsBusy = true;
            SetStatusMessage("Downloading ROMs...");
            ValidationMessage = string.Empty;

            var romFolder = PathHelper.ExpandOSEnvironmentVariables(_workingConfig.SystemConfig.EffectiveROMDirectory);
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
                    _logger.LogInformation(
                        "Downloading ROM {RomName} from {SourceUrl} to {Destination}",
                        romName,
                        romUrl,
                        dest);

                    using var response = await _httpClient.GetAsync(romUrl);
                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"Failed to get '{romUrl}' ({(int)response.StatusCode})");
                    await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                    _logger.LogInformation("Downloaded {Filename} to {Destination}", filename, dest);

                    // Update the C64SystemConfig with the downloaded ROM file
                    _workingConfig.SystemConfig.SetROM(romName, filename);
                }
                catch (Exception ex)
                {
                    if (File.Exists(dest))
                        File.Delete(dest);

                    var userMessage = DownloadErrorHelper.BuildDownloadFailureMessage(
                        $"ROM '{romName}'",
                        romUrl,
                        romUrl,
                        ex);

                    _logger.LogError(
                        ex,
                        "Failed to download ROM {RomName}. Source URL: {SourceUrl}. Destination: {Destination}. Details: {ErrorSummary}",
                        romName,
                        romUrl,
                        dest,
                        DownloadErrorHelper.FlattenExceptionMessages(ex));

                    throw new InvalidOperationException(userMessage, ex);
                }
            }

            SetStatusMessage("ROMs downloaded successfully.");

        }
        catch (System.Exception ex)
        {
            _logger.LogError(
                ex,
                "C64 ROM download to files failed. Details: {ErrorSummary}",
                DownloadErrorHelper.FlattenExceptionMessages(ex));
            SetStatusMessage(ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
            RomStatuses.Clear();
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
            SetStatusMessage("ROM files loaded.");
            ValidationMessage = string.Empty;
        }
        else
        {
            SetStatusMessage(errors.Count < romDataList.Count() ? "Some ROMs loaded with warnings." : null);
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
            SetStatusMessage("ROM files loaded.");
            ValidationMessage = string.Empty;
        }
        else
        {
            SetStatusMessage(errors.Count < filePaths.Count() ? "Some ROMs loaded with warnings." : null);
            ValidationMessage = string.Join(Environment.NewLine, errors);
        }
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
                ValidationMessage = string.Join(Environment.NewLine, validationErrors); ;
                return false;
            }

            ApplyWorkingConfigToOriginal();

            _hostApp.UpdateHostSystemConfig(_originalConfig);
            await _hostApp.PersistCurrentHostSystemConfig();

            var openAIConfigSection = _openAIConfig.GetConfigurationSection(_configuration);
            var openAISelfhostedConfigSection = _openAISelfHostedConfig.GetConfigurationSection(_configuration);
            var customEndpointConfigSection = _customEndpointConfig.GetConfigurationSection(_configuration);
            await _hostApp.PersistConfigSectionAsync(ApiConfig.CONFIG_SECTION, openAIConfigSection);
            await _hostApp.PersistConfigSectionAsync(ApiConfig.CONFIG_SECTION_SELF_HOSTED, openAISelfhostedConfigSection);
            await _hostApp.PersistConfigSectionAsync(CustomAIEndpointConfig.CONFIG_SECTION, customEndpointConfigSection);

            SetStatusMessage("Configuration saved.");
            return true;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving C64 configuration.");
            SetStatusMessage($"Error saving config: {ex.Message}", isError: true);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // AI Coding Assistant properties
    private CodeSuggestionBackendTypeOption? _selectedAIBackendTypeOption;

    public ObservableCollection<CodeSuggestionBackendTypeOption> AIBackendTypes { get; } = new()
    {
        new CodeSuggestionBackendTypeOption(CodeSuggestionBackendTypeEnum.None, "None"),
        new CodeSuggestionBackendTypeOption(CodeSuggestionBackendTypeEnum.OpenAI, "OpenAI"),
        new CodeSuggestionBackendTypeOption(CodeSuggestionBackendTypeEnum.OpenAISelfHostedCodeLlama, "OpenAI Self-Hosted (Ollama)"),
        new CodeSuggestionBackendTypeOption(CodeSuggestionBackendTypeEnum.CustomEndpoint, "Custom Endpoint")
    };

    public CodeSuggestionBackendTypeOption? SelectedAIBackendTypeOption
    {
        get => _selectedAIBackendTypeOption;
        set
        {
            if (ReferenceEquals(_selectedAIBackendTypeOption, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedAIBackendTypeOption, value);
            if (value != null)
            {
                SelectedAIBackendType = value.Type;
            }
        }
    }

    public CodeSuggestionBackendTypeEnum SelectedAIBackendType
    {
        get => _selectedAIBackendType;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedAIBackendType, value);
            _workingConfig.CodeSuggestionBackendType = value;

            // Update the selected option to match
            var matchingOption = AIBackendTypes.FirstOrDefault(o => o.Type == value);
             if (matchingOption != null && !ReferenceEquals(_selectedAIBackendTypeOption, matchingOption))
            {
                _selectedAIBackendTypeOption = matchingOption;
                this.RaisePropertyChanged(nameof(SelectedAIBackendTypeOption));
            }

            AITestStatusMessage = string.Empty;
            AITestStatusIsSuccess = false;
            this.RaisePropertyChanged(nameof(ShowOpenAISettings));
            this.RaisePropertyChanged(nameof(ShowSelfHostedSettings));
            this.RaisePropertyChanged(nameof(ShowCustomEndpointSettings));
            this.RaisePropertyChanged(nameof(ShowAITestButton));
        }
    }

    public bool ShowOpenAISettings => SelectedAIBackendType == CodeSuggestionBackendTypeEnum.OpenAI;
    public bool ShowSelfHostedSettings => SelectedAIBackendType == CodeSuggestionBackendTypeEnum.OpenAISelfHostedCodeLlama;
    public bool ShowCustomEndpointSettings => SelectedAIBackendType == CodeSuggestionBackendTypeEnum.CustomEndpoint;
    public bool ShowAITestButton => SelectedAIBackendType != CodeSuggestionBackendTypeEnum.None;

    public string OpenAIApiKey
    {
        get => _openAIConfig.ApiKey ?? string.Empty;
        set
        {
            if (_openAIConfig.ApiKey == value)
                return;
            _openAIConfig.ApiKey = value;
            this.RaisePropertyChanged();
        }
    }

    public string OpenAISelfHostedEndpoint
    {
        get => _openAISelfHostedConfig.EndpointString;
        set
        {
            if (_openAISelfHostedConfig.EndpointString == value)
                return;
            _openAISelfHostedConfig.EndpointString = value;
            this.RaisePropertyChanged();
        }
    }

    public string OpenAISelfHostedModelName
    {
        get => _openAISelfHostedConfig.DeploymentName ?? string.Empty;
        set
        {
            if (_openAISelfHostedConfig.DeploymentName == value)
                return;
            _openAISelfHostedConfig.DeploymentName = value;
            this.RaisePropertyChanged();
        }
    }

    public string OpenAISelfHostedApiKey
    {
        get => _openAISelfHostedConfig.ApiKey ?? string.Empty;
        set
        {
            if (_openAISelfHostedConfig.ApiKey == value)
                return;
            _openAISelfHostedConfig.ApiKey = value;
            this.RaisePropertyChanged();
        }
    }

    public string CustomEndpointApiKey
    {
        get => _customEndpointConfig.ApiKey ?? string.Empty;
        set
        {
            if (_customEndpointConfig.ApiKey == value)
                return;
            _customEndpointConfig.ApiKey = value;
            this.RaisePropertyChanged();
        }
    }

    public string CustomEndpoint
    {
        get => _customEndpointConfig.Endpoint?.OriginalString ?? string.Empty;
        set
        {
            var newUri = string.IsNullOrWhiteSpace(value) ? null : new Uri(value);
            if (_customEndpointConfig.Endpoint == newUri)
                return;
            _customEndpointConfig.Endpoint = newUri;
            this.RaisePropertyChanged();
        }
    }

    public string AITestStatusMessage
    {
        get => _aiTestStatusMessage;
        set => this.RaiseAndSetIfChanged(ref _aiTestStatusMessage, value);
    }

    public bool AITestStatusIsSuccess
    {
        get => _aiTestStatusIsSuccess;
        set => this.RaiseAndSetIfChanged(ref _aiTestStatusIsSuccess, value);
    }

    public string AIHelpUrl => "https://highbyte.github.io/dotnet-6502/docs/systems/c64/code-completion/";

    private void LoadAIConfiguration()
    {
        // Load AI settings from configuration object
        _openAIConfig = new ApiConfig(_configuration, selfHosted: false);
        _openAISelfHostedConfig = new ApiConfig(_configuration, selfHosted: true);
        _customEndpointConfig = new CustomAIEndpointConfig(_configuration);

        // Raise property changed for all UI-bound properties
        this.RaisePropertyChanged(nameof(OpenAIApiKey));
        this.RaisePropertyChanged(nameof(OpenAISelfHostedEndpoint));
        this.RaisePropertyChanged(nameof(OpenAISelfHostedModelName));
        this.RaisePropertyChanged(nameof(OpenAISelfHostedApiKey));
        this.RaisePropertyChanged(nameof(CustomEndpointApiKey));
        this.RaisePropertyChanged(nameof(CustomEndpoint));
    }

    private void WriteAIConfiguration()
    {
        // Write AI settings to configuration object
        _openAIConfig.WriteToConfiguration(_configuration);
        _openAISelfHostedConfig.WriteToConfiguration(_configuration);
        _customEndpointConfig.WriteToConfiguration(_configuration);
    }

    private async Task TestAIBackendAsync()
    {
        try
        {
            IsBusy = true;

            AITestStatusMessage = "Testing...";
            AITestStatusIsSuccess = false;

            ICodeSuggestion codeSuggestion;

            if (SelectedAIBackendType == CodeSuggestionBackendTypeEnum.OpenAI)
            {
                codeSuggestion = OpenAICodeSuggestion.CreateOpenAICodeSuggestion(
                    _openAIConfig,
                    _loggerFactory,
                    C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION,
                    C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION);
            }
            else if (SelectedAIBackendType == CodeSuggestionBackendTypeEnum.OpenAISelfHostedCodeLlama)
            {
                codeSuggestion = OpenAICodeSuggestion.CreateOpenAICodeSuggestionForCodeLlama(
                    _openAISelfHostedConfig,
                    _loggerFactory,
                    C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION,
                    C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION);
            }
            else if (SelectedAIBackendType == CodeSuggestionBackendTypeEnum.CustomEndpoint)
            {
                codeSuggestion = new CustomAIEndpointCodeSuggestion(
                    _customEndpointConfig,
                    _loggerFactory,
                    C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION);
            }
            else
            {
                AITestStatusMessage = "No backend selected to test.";
                AITestStatusIsSuccess = false;
                return;
            }

            await codeSuggestion.CheckAvailability();

            if (codeSuggestion.IsAvailable)
            {
                AITestStatusMessage = "OK";
                AITestStatusIsSuccess = true;
            }
            else
            {
                AITestStatusMessage = codeSuggestion.LastError ?? "Error";
                AITestStatusIsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            AITestStatusMessage = $"Error: {ex.Message}";
            AITestStatusIsSuccess = false;
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

        if (SelectedRenderProvider != null)
        {
            _workingConfig.SystemConfig.SetRenderProviderType(SelectedRenderProvider.Type);
        }
    }

    private void InitializeSidEmulationModeOptions()
    {
        SidEmulationModes.Clear();
        SidEmulationModes.Add(new SidEmulationModeOption(
            SidEmulationMode.Auto,
            "Accurate (auto)",
            "Full SID emulation with combined waveforms, hard sync, ring modulation, OSC3/ENV3 readback and the SID filter (low/band/high-pass with resonance). Inner loop takes fast paths automatically when the current SID state doesn't need those features."));
        SidEmulationModes.Add(new SidEmulationModeOption(
            SidEmulationMode.Fast,
            "Fast",
            "Lower CPU. Disables combined waveforms, hard sync, ring modulation, TEST-bit hold, OSC3/ENV3 readback and the filter regardless of SID state. Many tunes will sound wrong."));

        SelectedSidEmulationMode = SidEmulationModes.FirstOrDefault(o => o.Mode == _workingConfig.SystemConfig.SidEmulationMode)
            ?? SidEmulationModes.First();

        if (SelectedSidEmulationMode != null)
            _workingConfig.SystemConfig.SidEmulationMode = SelectedSidEmulationMode.Mode;
    }

    private void InitializeAudioOptions()
    {
        AudioProviders.Clear();

        var providerTypes = _audioCombinations.Select(c => c.audioProviderType).Distinct().ToList();
        if (_workingConfig.SystemConfig.AudioProviderType != null && !providerTypes.Contains(_workingConfig.SystemConfig.AudioProviderType))
        {
            providerTypes.Add(_workingConfig.SystemConfig.AudioProviderType);
        }

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
        {
            _workingConfig.SystemConfig.SetAudioProviderType(SelectedAudioProvider.Type);
        }
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
            {
                _workingConfig.SystemConfig.SetAudioTargetType(SelectedAudioTarget.Type);
            }
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

        // Notify that RomStatuses collection has been updated
        this.RaisePropertyChanged(nameof(RomStatuses));
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

        if (_workingConfig.InputConfig.KeyboardToC64JoystickMap.TryGetValue(SelectedHostJoystick, out var map))
        {
            foreach (var kvp in map)
            {
                KeyboardMappings.Add(new KeyMappingEntry(kvp.Key.ToString(), kvp.Value.ToString()));
            }
        }
    }

    private void UpdateValidationMessageFromConfig()
    {
        var validationErrors = new List<string>();
        if (!_workingConfig.IsValid(out validationErrors))
        {
            // validationErrors already populated by host/system config validation
        }

        var swiftLinkPortInputError = GetSwiftLinkPortInputValidationError();
        if (!string.IsNullOrEmpty(swiftLinkPortInputError))
            validationErrors.Add(swiftLinkPortInputError);

        if (validationErrors.Count > 0)
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

    private string? GetSwiftLinkPortInputValidationError()
    {
        if (IsRunningInWebAssembly)
            return null;

        if (string.IsNullOrWhiteSpace(SwiftLinkTcpPortText))
            return $"{nameof(C64HostConfig.SwiftLinkHost)}.{nameof(C64SwiftLinkHostConfig.TcpPort)} must be between 1 and 65535.";

        if (!int.TryParse(SwiftLinkTcpPortText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort))
            return $"{nameof(C64HostConfig.SwiftLinkHost)}.{nameof(C64SwiftLinkHostConfig.TcpPort)} must be between 1 and 65535.";

        if (parsedPort is < 1 or > 65535)
            return $"{nameof(C64HostConfig.SwiftLinkHost)}.{nameof(C64SwiftLinkHostConfig.TcpPort)} must be between 1 and 65535.";

        return null;
    }

    private void ApplyWorkingConfigToOriginal()
    {
        _originalConfig.SystemConfig.ROMDirectory = _workingConfig.SystemConfig.ROMDirectory;
        _originalConfig.SystemConfig.AudioEnabled = _workingConfig.SystemConfig.AudioEnabled;
        _originalConfig.SystemConfig.KeyboardJoystickEnabled = _workingConfig.SystemConfig.KeyboardJoystickEnabled;
        _originalConfig.SystemConfig.KeyboardJoystick = _workingConfig.SystemConfig.KeyboardJoystick;
        _originalConfig.SystemConfig.SwiftLink = _workingConfig.SystemConfig.SwiftLink.Clone();
        _originalConfig.SystemConfig.ColorMapName = _workingConfig.SystemConfig.ColorMapName;
        _originalConfig.SwiftLinkHost = _workingConfig.SwiftLinkHost.Clone();
        _originalConfig.SwiftLinkWebSocketBridgeUrl = _workingConfig.SwiftLinkWebSocketBridgeUrl;
        _originalConfig.SwiftLinkSharedToken = _workingConfig.SwiftLinkSharedToken;
        _originalConfig.SwiftLinkBridgeTargetId = _workingConfig.SwiftLinkBridgeTargetId;
        _originalConfig.SwiftLinkBridgeTargetIds = new List<string>(_workingConfig.SwiftLinkBridgeTargetIds);

        if (_workingConfig.SystemConfig.RenderProviderType != null)
            _originalConfig.SystemConfig.SetRenderProviderType(_workingConfig.SystemConfig.RenderProviderType);

        if (_workingConfig.SystemConfig.RenderTargetType != null)
            _originalConfig.SystemConfig.SetRenderTargetType(_workingConfig.SystemConfig.RenderTargetType);

        if (_workingConfig.SystemConfig.AudioProviderType != null)
            _originalConfig.SystemConfig.SetAudioProviderType(_workingConfig.SystemConfig.AudioProviderType);

        if (_workingConfig.SystemConfig.AudioTargetType != null)
            _originalConfig.SystemConfig.SetAudioTargetType(_workingConfig.SystemConfig.AudioTargetType);

        _originalConfig.SystemConfig.CpuCompatibilityProfile = _workingConfig.SystemConfig.CpuCompatibilityProfile;
        _originalConfig.SystemConfig.SidEmulationMode = _workingConfig.SystemConfig.SidEmulationMode;

        _originalConfig.SystemConfig.ROMs = ROM.Clone(_workingConfig.SystemConfig.ROMs);
        _originalConfig.InputConfig = (C64InputConfig)_workingConfig.InputConfig.Clone();

        // Apply AI Coding Assistant settings
        _originalConfig.CodeSuggestionBackendType = _workingConfig.CodeSuggestionBackendType;
        _originalConfig.BasicAIAssistantDefaultEnabled = _workingConfig.BasicAIAssistantDefaultEnabled;
        WriteAIConfiguration();
    }

    /// <summary>
    /// Populates all bound view-model properties from the current <see cref="_workingConfig"/>.
    /// Called from the constructor and after <see cref="ResetToDefaults"/> swaps the working config.
    /// </summary>
    private void LoadFromWorkingConfig()
    {
        KeyboardJoystickEnabled = _workingConfig.SystemConfig.KeyboardJoystickEnabled;
        SelectedKeyboardJoystick = _workingConfig.SystemConfig.KeyboardJoystick;
        SelectedHostJoystick = _workingConfig.InputConfig.CurrentJoystick;
        SelectedKeyboardLayout = _workingConfig.InputConfig.KeyboardLayout?.ToString() ?? AutoKeyboardLayoutLabel;
        SwiftLinkEnabled = _workingConfig.SystemConfig.SwiftLink.Enabled;
        SelectedSwiftLinkCartridgeIOAddress = _workingConfig.SystemConfig.SwiftLink.CartridgeIOAddress;
        SelectedSwiftLinkTransportMode = _workingConfig.SwiftLinkHost.TransportMode;
        SelectedSwiftLinkInterruptMode = _workingConfig.SystemConfig.SwiftLink.InterruptMode;
        SelectedSwiftLinkReceiveMode = _workingConfig.SystemConfig.SwiftLink.ReceiveMode;
        SwiftLinkTcpHost = _workingConfig.SwiftLinkHost.TcpHost;
        SwiftLinkTcpPort = _workingConfig.SwiftLinkHost.TcpPort;
        SwiftLinkConnectOnBoot = _workingConfig.SwiftLinkHost.ConnectOnBoot;
        SwiftLinkBridgeWebSocketUrl = _workingConfig.SwiftLinkWebSocketBridgeUrl ?? string.Empty;
        SwiftLinkSharedToken = _workingConfig.SwiftLinkSharedToken ?? string.Empty;
        SwiftLinkBridgeTargetId = _workingConfig.SwiftLinkBridgeTargetId ?? string.Empty;
        InitializeSwiftLinkBridgeTargetOptions();

        AudioEnabled = _workingConfig.SystemConfig.AudioEnabled;
        Vic2RasterizerPerLineSprites = _workingConfig.SystemConfig.Vic2RasterizerPerLineSprites;
        SelectedCpuCompatibilityProfile = CpuCompatibilityProfileOption.FromProfile(_workingConfig.SystemConfig.CpuCompatibilityProfile);

        RomDirectory = _workingConfig.SystemConfig.ROMDirectory;

        // Initialize AI Coding Assistant properties
        BasicAIAssistantEnabled = _workingConfig.BasicAIAssistantDefaultEnabled;
        SelectedAIBackendType = _workingConfig.CodeSuggestionBackendType;
        LoadAIConfiguration();

        InitializeRenderOptions();
        InitializeAudioOptions();
        InitializeSidEmulationModeOptions();
        UpdateRomStatuses();
        UpdateKeyboardMappings();
        UpdateValidationMessageFromConfig();
    }

    /// <summary>
    /// Resets all settings to application defaults, while preserving the user's loaded ROMs and ROM
    /// directory (so they don't have to re-download or re-point ROM files). Separately stored AI
    /// assistant API keys are also preserved (they live in their own config sections and are reloaded
    /// by <see cref="LoadFromWorkingConfig"/> -> <see cref="LoadAIConfiguration"/>). Nothing is
    /// persisted until the user clicks Save.
    /// </summary>
    private void ResetToDefaults()
    {
        var preservedRoms = ROM.Clone(_workingConfig.SystemConfig.ROMs);
        var preservedRomDirectory = _workingConfig.SystemConfig.ROMDirectory;

        _workingConfig = new C64HostConfig();
        _workingConfig.SystemConfig.ROMs = preservedRoms;
        _workingConfig.SystemConfig.ROMDirectory = preservedRomDirectory;

        LoadFromWorkingConfig();

        SetStatusMessage("Settings reset to defaults. Click Save to apply.");
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

    private Task<bool> RequestRomLicenseAcknowledgementAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        
        var args = new RomLicenseAcknowledgementEventArgs(tcs);
        RomLicenseAcknowledgementRequested?.Invoke(this, args);
        
        return tcs.Task;
    }

    // Add event for ROM license acknowledgement request
    public event EventHandler<RomLicenseAcknowledgementEventArgs>? RomLicenseAcknowledgementRequested;

    private void InitializeSwiftLinkBridgeTargetOptions()
    {
        AvailableSwiftLinkBridgeTargets.Clear();
        AvailableSwiftLinkBridgeTargets.Add(new SwiftLinkBridgeTargetOption(DefaultSwiftLinkBridgeTargetLabel, null));

        foreach (var targetId in _workingConfig.SwiftLinkBridgeTargetIds
                     .Where(targetId => !string.IsNullOrWhiteSpace(targetId))
                     .Select(targetId => targetId.Trim())
                     .Distinct(StringComparer.Ordinal))
        {
            AvailableSwiftLinkBridgeTargets.Add(new SwiftLinkBridgeTargetOption(targetId, targetId));
        }

        if (!string.IsNullOrWhiteSpace(_workingConfig.SwiftLinkBridgeTargetId)
            && AvailableSwiftLinkBridgeTargets.All(option => option.Value != _workingConfig.SwiftLinkBridgeTargetId))
        {
            var targetId = _workingConfig.SwiftLinkBridgeTargetId.Trim();
            AvailableSwiftLinkBridgeTargets.Add(new SwiftLinkBridgeTargetOption(targetId, targetId));
        }

        SelectedSwiftLinkBridgeTarget = AvailableSwiftLinkBridgeTargets
            .FirstOrDefault(option => option.Value == _workingConfig.SwiftLinkBridgeTargetId)
            ?? AvailableSwiftLinkBridgeTargets.FirstOrDefault();
    }
}

public class RomLicenseAcknowledgementEventArgs : EventArgs
{
    private readonly TaskCompletionSource<bool> _taskCompletionSource;

    public RomLicenseAcknowledgementEventArgs(TaskCompletionSource<bool> taskCompletionSource)
    {
        _taskCompletionSource = taskCompletionSource;
    }

    public void SetResult(bool acknowledged)
    {
        _taskCompletionSource.TrySetResult(acknowledged);
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
        RomFile = data.RomFile;
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

public record AudioProviderOption(Type Type, string DisplayName, string HelpText);

public record AudioTargetOption(Type Type, string DisplayName, string HelpText);

public record SidEmulationModeOption(SidEmulationMode Mode, string DisplayName, string HelpText);

public record KeyMappingEntry(string Key, string Action);

public record CodeSuggestionBackendTypeOption(CodeSuggestionBackendTypeEnum Type, string DisplayName);

public record SwiftLinkBridgeTargetOption(string Label, string? Value);

internal readonly record struct RomStatusData(
    string Name,
    bool IsLoaded,
    bool IsRequired,
    string Details,
    string ForegroundColor,
    string RomFile);
