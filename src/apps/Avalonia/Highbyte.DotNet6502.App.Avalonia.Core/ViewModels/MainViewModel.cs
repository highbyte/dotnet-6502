using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Impl.Avalonia.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly ILogger _logger;

    // Expose HostApp for EmulatorView that currently needs it (TODO: Consider removing this dependency via MainViewModel. Better that EmulatorViewModel provides it.)
    public AvaloniaHostApp HostApp => _hostApp;

    // Child ViewModels exposed as properties for XAML binding
    public C64MenuViewModel C64MenuViewModel { get; }
    public C64InfoViewModel C64InfoViewModel { get; }
    public StatisticsViewModel StatisticsViewModel { get; }
    public EmulatorViewModel EmulatorViewModel { get; }
    public EmulatorPlaceholderViewModel EmulatorPlaceholderViewModel { get; }

    // --- Start Binding Properties ---

    // Read-only properties derived from HostApp
    private readonly ObservableAsPropertyHelper<string> _selectedSystemName;
    public string SelectedSystemName => _selectedSystemName.Value;

    private readonly ObservableAsPropertyHelper<string> _selectedSystemConfigurationVariant;
    public string SelectedSystemVariant => _selectedSystemConfigurationVariant.Value;


    private readonly ObservableAsPropertyHelper<ObservableCollection<string>> _availableSystems;
    public ObservableCollection<string> AvailableSystems => _availableSystems.Value;

    private readonly ObservableAsPropertyHelper<ObservableCollection<string>> _availableSystemVariants;
    public ObservableCollection<string> AvailableSystemVariants => _availableSystemVariants.Value;


    private readonly ObservableAsPropertyHelper<EmulatorState> _emulatorState;
    public EmulatorState EmulatorState => _emulatorState.Value;

    private readonly ObservableAsPropertyHelper<bool> _isExternalDebuggerAttached;
    public bool IsExternalDebuggerAttached => _isExternalDebuggerAttached.Value;

    private readonly ObservableAsPropertyHelper<double> _scale;
    public double Scale
    {
        get => _scale.Value;
        set
        {
            _hostApp.Scale = (float)value;
        }
    }

    public bool IsC64SystemSelected => string.Equals(SelectedSystemName, C64.SystemName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Currently-active system menu contributor (supplies macOS native menu + keyboard shortcuts).
    /// Swaps when <see cref="SelectedSystemName"/> changes; null when no system is selected.
    /// </summary>
    private ISystemMenuContributor? _activeMenuContributor;
    public ISystemMenuContributor? ActiveMenuContributor
    {
        get => _activeMenuContributor;
        private set => this.RaiseAndSetIfChanged(ref _activeMenuContributor, value);
    }

    // Computed properties for control enabled states based on EmulatorState
    public bool IsEmulatorRunning => EmulatorState == EmulatorState.Running;
    public bool IsEmulatorPaused => EmulatorState == EmulatorState.Paused;
    public bool IsEmulatorUninitialized => EmulatorState == EmulatorState.Uninitialized;

    public string StatusEmulatorStateText => EmulatorState switch
    {
        EmulatorState.Running => "Running",
        EmulatorState.Paused => "Paused",
        _ => "Idle",
    };

    public string StatusSystemText
    {
        get
        {
            var name = SelectedSystemName;
            var variant = SelectedSystemVariant;
            if (string.IsNullOrEmpty(name) || name == "DEFAULT SYSTEM")
                return string.Empty;
            if (string.IsNullOrEmpty(variant) || variant == "DEFAULT VARIANT")
                return name;
            return $"{name} ({variant})";
        }
    }

    private string _statusFpsText = string.Empty;
    public string StatusFpsText
    {
        get => _statusFpsText;
        private set => this.RaiseAndSetIfChanged(ref _statusFpsText, value);
    }

    private global::Avalonia.Threading.DispatcherTimer? _statusFpsTimer;

    // Debug tab visibility from config
    public bool IsDebugTabVisible => _emulatorConfig.ShowDebugTools || PlatformDetection.IsRunningOnDesktop();
    public bool IsDebugToolsVisible => _emulatorConfig.ShowDebugTools;

    // Private field to cache validation errors
    private readonly ObservableAsPropertyHelper<ObservableCollection<string>> _validationErrors;
    public ObservableCollection<string> ValidationErrors => _validationErrors.Value;

    // Computed property that updates when ValidationErrors changes
    private readonly ObservableAsPropertyHelper<bool> _hasValidationErrors;
    public bool HasValidationErrors => _hasValidationErrors.Value;

    // Private field for log messages collection
    private readonly ObservableCollection<LogDisplayEntry> _logMessages = new();
    public ObservableCollection<LogDisplayEntry> LogMessages => _logMessages;

    // Log tab header properties
    private string _logTabHeader = "Log";
    public string LogTabHeader
    {
        get => _logTabHeader;
        private set => this.RaiseAndSetIfChanged(ref _logTabHeader, value);
    }

    private bool _hasLogErrors = false;
    public bool HasLogErrors
    {
        get => _hasLogErrors;
        private set => this.RaiseAndSetIfChanged(ref _hasLogErrors, value);
    }

    // Scripts tab properties
    private string _scriptsTabHeader = "Scripts";
    public string ScriptsTabHeader
    {
        get => _scriptsTabHeader;
        private set => this.RaiseAndSetIfChanged(ref _scriptsTabHeader, value);
    }

    private bool _hasDisabledScripts = false;
    public bool HasDisabledScripts
    {
        get => _hasDisabledScripts;
        private set => this.RaiseAndSetIfChanged(ref _hasDisabledScripts, value);
    }

    private bool _isScriptingEnabled;
    public bool IsScriptingEnabled
    {
        get => _isScriptingEnabled;
        private set => this.RaiseAndSetIfChanged(ref _isScriptingEnabled, value);
    }

    private readonly ObservableCollection<ScriptDisplayEntry> _scriptEntries = new();
    public ObservableCollection<ScriptDisplayEntry> ScriptEntries => _scriptEntries;

    private ScriptSortColumn _scriptSortColumn = ScriptSortColumn.FileName;
    private bool _scriptSortAscending = true;

    public string FileNameSortIndicator => SortIndicator(ScriptSortColumn.FileName);
    public string StatusSortIndicator   => SortIndicator(ScriptSortColumn.Status);
    public string YieldSortIndicator    => SortIndicator(ScriptSortColumn.YieldType);
    public string HooksSortIndicator    => SortIndicator(ScriptSortColumn.Hooks);

    private string SortIndicator(ScriptSortColumn col) =>
        _scriptSortColumn == col ? (_scriptSortAscending ? " ▲" : " ▼") : "";

    public bool CanManageScripts { get; }
    public bool CanLoadExamples { get; }

    // Show the script directory info banner on Desktop (not browser) when scripting is enabled
    public bool ShowScriptDirectoryInfo => IsScriptingEnabled && !CanManageScripts;
    public string ScriptDirectory => _hostApp.ScriptDirectory;

    // Tab tracking for performance optimization
    private string _selectedTabName = "";
    public string SelectedTabName
    {
        get => _selectedTabName;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTabName, value);

            // Notify that log tab visibility may have changed
            this.RaisePropertyChanged(nameof(IsLogTabVisible));

            // If log tab just became visible and there are pending updates, trigger immediate update
            if (IsLogTabVisible)
            {
                lock (_logUpdateLock)
                {
                    if (_hasPendingLogUpdates)
                    {
                        // Copy new messages from backing store to UI collection
                        var newMessages = _logMessagesBackingStore.Skip(_logMessages.Count).ToList();
                        _hasPendingLogUpdates = false;

                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                // Add new messages to UI collection
                                foreach (var message in newMessages)
                                {
                                    _logMessages.Add(message);
                                }
                                UpdateLogTabHeader();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error updating log UI on tab switch");
                            }
                        });
                    }
                }
            }
        }
    }

    // Check if log tab is currently visible by name
    public bool IsLogTabVisible => string.Equals(_selectedTabName, "LogTabItem", StringComparison.OrdinalIgnoreCase);

    // Timer for batched UI updates
    private Timer? _logUpdateTimer;
    private readonly object _logUpdateLock = new object();
    private bool _hasPendingLogUpdates = false;

    // Thread-safe backing collection for log messages
    private readonly List<LogDisplayEntry> _logMessagesBackingStore = new();

    // Statistics panel visibility
    private readonly ObservableAsPropertyHelper<bool> _isStatisticsPanelVisible;
    public bool IsStatisticsPanelVisible => _isStatisticsPanelVisible.Value;
    // Statistics panel column width - bind this to the grid column width
    public string StatisticsPanelColumnWidth => "250";

    // Monitor visibility - watches Monitor.IsVisible directly
    private bool _isMonitorVisible;
    private AvaloniaMonitor? _currentMonitor; // Track current monitor for proper event unsubscription
    public bool IsMonitorVisible
    {
        get => _isMonitorVisible;
        private set => this.RaiseAndSetIfChanged(ref _isMonitorVisible, value);
    }

    // Monitor ViewModel - created when monitor is enabled
    private MonitorViewModel? _monitorViewModel;
    public MonitorViewModel? MonitorViewModel
    {
        get
        {
            // Lazy creation: if monitor is visible but ViewModel is null, create it
            if (_monitorViewModel == null && IsMonitorVisible)
            {
                _monitorViewModel = CreateMonitorViewModel();
            }
            return _monitorViewModel;
        }
        private set => this.RaiseAndSetIfChanged(ref _monitorViewModel, value);
    }

    // Audio properties - track AudioSupported and AudioEnabled from HostApp
    private readonly ObservableAsPropertyHelper<bool> _audioSupported;
    public bool AudioSupported => _audioSupported.Value;
    public bool AudioSettingsEnabled => AudioSupported && EmulatorState == EmulatorState.Uninitialized;

    // AudioEnabled - two-way binding property
    private bool _audioEnabled;
    public bool AudioEnabled
    {
        get => _audioEnabled;
        set
        {
            if (_audioEnabled != value)
            {
                this.RaiseAndSetIfChanged(ref _audioEnabled, value);
                SafeAsyncHelper.Execute(() => _hostApp.SetAudioEnabled(value));
            }
        }
    }

    private readonly ObservableAsPropertyHelper<string?> _audioTooltip;
    public string? AudioTooltip => _audioTooltip.Value;

    // Audio volume property - two-way binding
    private double _audioVolumePercent = 20.0;
    public double AudioVolumePercent
    {
        get => _audioVolumePercent;
        set
        {
            this.RaiseAndSetIfChanged(ref _audioVolumePercent, value);
            _hostApp.SetVolumePercent((float)value);
        }
    }

    // External debug server properties (Desktop only; null controller → all false/zero)
    private readonly IExternalDebugController? _externalDebugController;

    public bool IsExternalDebugServerAvailable => _externalDebugController != null;

    private bool _isExternalDebugListening;
    public bool IsExternalDebugListening
    {
        get => _isExternalDebugListening;
        private set => this.RaiseAndSetIfChanged(ref _isExternalDebugListening, value);
    }

    private bool _isExternalDebugClientConnected;
    public bool IsExternalDebugClientConnected
    {
        get => _isExternalDebugClientConnected;
        private set => this.RaiseAndSetIfChanged(ref _isExternalDebugClientConnected, value);
    }

    private int _externalDebugPort = 6502;
    private string _externalDebugPortText = "6502";
    public int ExternalDebugPort
    {
        get => _externalDebugPort;
        set => this.RaiseAndSetIfChanged(ref _externalDebugPort, value);
    }

    public string ExternalDebugPortText
    {
        get => _externalDebugPortText;
        set => SetPortText(value, ref _externalDebugPortText, nameof(ExternalDebugPortText), nameof(IsExternalDebugPortValid), nameof(ExternalDebugPortValidationMessage), nameof(ExternalDebugPortInputToolTip), port => ExternalDebugPort = port);
    }

    public bool IsExternalDebugPortValid => TryParsePortText(_externalDebugPortText, out _);
    public string? ExternalDebugPortValidationMessage => IsExternalDebugPortValid ? null : "Enter a TCP port from 1 to 65535.";
    public string ExternalDebugPortInputToolTip => ExternalDebugPortValidationMessage ?? "TCP port for the debug adapter server (default: 6502).";

    private string _externalDebugBindAddress = IExternalDebugController.DefaultBindAddress;
    private string _externalDebugBindAddressText = IExternalDebugController.DefaultBindAddress;
    public string ExternalDebugBindAddress
    {
        get => _externalDebugBindAddress;
        set => this.RaiseAndSetIfChanged(ref _externalDebugBindAddress, value);
    }

    public string ExternalDebugBindAddressText
    {
        get => _externalDebugBindAddressText;
        set => SetIpv4Text(value, ref _externalDebugBindAddressText, nameof(ExternalDebugBindAddressText), nameof(IsExternalDebugBindAddressValid), nameof(ExternalDebugBindAddressValidationMessage), nameof(ExternalDebugBindAddressInputToolTip), bindAddress => ExternalDebugBindAddress = bindAddress);
    }

    public bool IsExternalDebugBindAddressValid => IsValidIpv4Address(_externalDebugBindAddressText);
    public string? ExternalDebugBindAddressValidationMessage => IsExternalDebugBindAddressValid ? null : "Enter an IPv4 address as four groups of digits from 0 to 255 separated by periods.";
    public string ExternalDebugBindAddressInputToolTip => ExternalDebugBindAddressValidationMessage ?? "IP address to bind the debug adapter server to (default: 127.0.0.1 for loopback only; use 0.0.0.0 to accept connections from any network interface; the debug adapter is unauthenticated and exposes debugging control).";

    public string ExternalDebugStatusText => _externalDebugController switch
    {
        null => "",
        { IsClientConnected: true } => "Connected",
        { IsListening: true } => $"Listening on {_externalDebugController.BindAddress}:{_externalDebugController.Port}",
        _ => "Off"
    };

    public string ExternalDebugToggleButtonText => _isExternalDebugListening ? "Stop" : "Start";

    public ReactiveCommand<Unit, Unit> ToggleExternalDebugCommand { get; }

    // Remote control server properties (Desktop only; null controller → all false/zero)
    private readonly IRemoteControlController? _remoteControlController;

    public bool IsRemoteControlAvailable => _remoteControlController != null;

    private bool _isRemoteControlListening;
    public bool IsRemoteControlListening
    {
        get => _isRemoteControlListening;
        private set => this.RaiseAndSetIfChanged(ref _isRemoteControlListening, value);
    }

    private bool _isRemoteClientConnected;
    public bool IsRemoteClientConnected
    {
        get => _isRemoteClientConnected;
        private set => this.RaiseAndSetIfChanged(ref _isRemoteClientConnected, value);
    }

    private double _remoteClientIndicatorOpacity;
    public double RemoteClientIndicatorOpacity
    {
        get => _remoteClientIndicatorOpacity;
        private set => this.RaiseAndSetIfChanged(ref _remoteClientIndicatorOpacity, value);
    }

    private static readonly TimeSpan RemoteClientIndicatorMinimumVisibleDuration = TimeSpan.FromSeconds(1);
    private System.Threading.CancellationTokenSource? _remoteClientIndicatorCts;
    private DateTimeOffset? _remoteClientIndicatorShownAtUtc;

    public double ExternalDebuggerIndicatorOpacity => IsExternalDebuggerAttached ? 1.0 : 0.0;

    private int _remoteControlPort = IRemoteControlController.DefaultPort;
    private string _remoteControlPortText = IRemoteControlController.DefaultPort.ToString(CultureInfo.InvariantCulture);
    public int RemoteControlPort
    {
        get => _remoteControlPort;
        set => this.RaiseAndSetIfChanged(ref _remoteControlPort, value);
    }

    public string RemoteControlPortText
    {
        get => _remoteControlPortText;
        set => SetPortText(value, ref _remoteControlPortText, nameof(RemoteControlPortText), nameof(IsRemoteControlPortValid), nameof(RemoteControlPortValidationMessage), nameof(RemoteControlPortInputToolTip), port => RemoteControlPort = port);
    }

    public bool IsRemoteControlPortValid => TryParsePortText(_remoteControlPortText, out _);
    public string? RemoteControlPortValidationMessage => IsRemoteControlPortValid ? null : "Enter a TCP port from 1 to 65535.";
    public string RemoteControlPortInputToolTip => RemoteControlPortValidationMessage ?? "TCP port for the remote control server (default: 6510).";

    private string _remoteControlBindAddress = IRemoteControlController.DefaultBindAddress;
    private string _remoteControlBindAddressText = IRemoteControlController.DefaultBindAddress;
    public string RemoteControlBindAddress
    {
        get => _remoteControlBindAddress;
        set => this.RaiseAndSetIfChanged(ref _remoteControlBindAddress, value);
    }

    public string RemoteControlBindAddressText
    {
        get => _remoteControlBindAddressText;
        set => SetIpv4Text(value, ref _remoteControlBindAddressText, nameof(RemoteControlBindAddressText), nameof(IsRemoteControlBindAddressValid), nameof(RemoteControlBindAddressValidationMessage), nameof(RemoteControlBindAddressInputToolTip), bindAddress => RemoteControlBindAddress = bindAddress);
    }

    public bool IsRemoteControlBindAddressValid => IsValidIpv4Address(_remoteControlBindAddressText);
    public string? RemoteControlBindAddressValidationMessage => IsRemoteControlBindAddressValid ? null : "Enter an IPv4 address as four groups of digits from 0 to 255 separated by periods.";
    public string RemoteControlBindAddressInputToolTip => RemoteControlBindAddressValidationMessage ?? "IP address to bind to (default: 127.0.0.1 for loopback only; use 0.0.0.0 to accept connections from any network interface; the protocol is unauthenticated).";

    public string RemoteControlStatusText => _remoteControlController switch
    {
        null => "",
        { IsListening: true } => $"Listening on {_remoteControlController.BindAddress}:{_remoteControlController.Port}",
        _ => "Off"
    };

    public string RemoteControlToggleButtonText => _isRemoteControlListening ? "Stop" : "Start";

    public ReactiveCommand<Unit, Unit> ToggleRemoteControlCommand { get; }

    public void ClearMonitorViewModel()
    {
        MonitorViewModel = null;
    }

    /// <summary>
    /// Refreshes config-dependent properties after configuration changes.
    /// Call this after saving emulator configuration.
    /// </summary>
    public void RefreshConfigProperties()
    {
        this.RaisePropertyChanged(nameof(IsDebugTabVisible));
        this.RaisePropertyChanged(nameof(IsDebugToolsVisible));
    }

    // --- End Binding Properties ---

    // --- ReactiveUI Commands ---
    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }
    public ReactiveCommand<Unit, Unit> MonitorCommand { get; }
    public ReactiveCommand<Unit, Unit> StatsCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearLogCommand { get; }
    public ReactiveCommand<string, Unit> SelectSystemCommand { get; }
    public ReactiveCommand<string, Unit> SelectSystemVariantCommand { get; }
    public ReactiveCommand<string, Unit> ToggleScriptEnabledCommand { get; }
    public ReactiveCommand<string, Unit> ReloadScriptCommand { get; }
    public ReactiveCommand<Unit, Unit> AddScriptCommand { get; }
    public ReactiveCommand<string, Unit> EditScriptCommand { get; }
    public ReactiveCommand<string, Unit> DeleteScriptCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadExamplesCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshScriptsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenScriptFolderCommand { get; }
    public ReactiveCommand<ScriptSortColumn, Unit> SortByColumnCommand { get; }

    // Events for script editor dialog (UI operation handled in View code-behind)
    public event EventHandler? RequestAddScript;
    public event EventHandler<string>? RequestEditScript;
    public event EventHandler<DeleteScriptConfirmationEventArgs>? RequestDeleteScript;
    public event EventHandler? RequestOpenScriptFolder;

    // Event for requesting the emulator options overlay (UI operation handled in View)
    public event EventHandler? EmulatorOptionsRequested;

    public ReactiveCommand<Unit, Unit> EmulatorOptionsCommand { get; }

    // --- End ReactiveUI Commands ---

    //public string Version => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
    public string Version => Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    // New properties to display runtime versions
    public string DotNetVersion => RuntimeInformation.FrameworkDescription;

    public string AvaloniaVersion
    {
        get
        {
            try
            {
                var asm = typeof(global::Avalonia.Controls.Control).Assembly;
                var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return infoAttr?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    public string OSVersion => RuntimeInformation.OSDescription;

    // Constructor with dependency injection - child ViewModels injected!
    public MainViewModel(
        AvaloniaHostApp hostApp,
        EmulatorConfig emulatorConfig,
        C64MenuViewModel c64MenuViewModel,
        C64InfoViewModel c64InfoViewModel,
        StatisticsViewModel statisticsViewModel,
        EmulatorViewModel emulatorViewModel,
        EmulatorPlaceholderViewModel emulatorPlaceholderViewModel,
        ILoggerFactory loggerFactory)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _emulatorConfig = emulatorConfig;
        _logger = loggerFactory?.CreateLogger(nameof(MainViewModel)) ?? throw new ArgumentNullException(nameof(loggerFactory));

        // Store injected child ViewModels
        C64MenuViewModel = c64MenuViewModel ?? throw new ArgumentNullException(nameof(c64MenuViewModel));
        C64InfoViewModel = c64InfoViewModel ?? throw new ArgumentNullException(nameof(c64InfoViewModel));
        StatisticsViewModel = statisticsViewModel ?? throw new ArgumentNullException(nameof(statisticsViewModel));
        EmulatorViewModel = emulatorViewModel ?? throw new ArgumentNullException(nameof(emulatorViewModel));
        EmulatorPlaceholderViewModel = emulatorPlaceholderViewModel ?? throw new ArgumentNullException(nameof(emulatorPlaceholderViewModel));

        // Set up reactive properties - all derived from HostApp (read-only)
        _selectedSystemName = _hostApp
            .WhenAnyValue(x => x.SelectedSystemName)
            .ToProperty(this, x => x.SelectedSystemName);

        _selectedSystemConfigurationVariant = _hostApp
            .WhenAnyValue(x => x.SelectedSystemConfigurationVariant)
            .ToProperty(this, x => x.SelectedSystemVariant);

        _availableSystems = _hostApp
            .WhenAnyValue(x => x.AvailableSystemNames)
            .Select(systems => new ObservableCollection<string>(systems))
            .ToProperty(this, x => x.AvailableSystems);

        _availableSystemVariants = _hostApp
            .WhenAnyValue(x => x.AllSelectedSystemConfigurationVariants)
            .Select(variants => new ObservableCollection<string>(variants))
            .ToProperty(this, x => x.AvailableSystemVariants);

        _emulatorState = _hostApp
            .WhenAnyValue(x => x.EmulatorState)
            .ToProperty(this, x => x.EmulatorState);

        _isExternalDebuggerAttached = _hostApp
            .WhenAnyValue(x => x.IsExternalDebuggerAttached)
            .ToProperty(this, x => x.IsExternalDebuggerAttached);

        this.WhenAnyValue(x => x.IsExternalDebuggerAttached)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ExternalDebuggerIndicatorOpacity)));

        _remoteClientIndicatorOpacity = _isRemoteClientConnected ? 1.0 : 0.0;
        if (_isRemoteClientConnected)
            _remoteClientIndicatorShownAtUtc = DateTimeOffset.UtcNow;

        this.WhenAnyValue(x => x.IsRemoteClientConnected)
            .Skip(1)
            .Subscribe(connected => _ = UpdateRemoteClientIndicatorAsync(connected));

        // Subscribe to EmulatorState changes AFTER ToProperty to ensure the value is updated first
        this.WhenAnyValue(x => x.EmulatorState)
             .Subscribe(_ =>
              {
                  // Notify all computed properties that depend on EmulatorState
                  this.RaisePropertyChanged(nameof(IsEmulatorRunning));
                  this.RaisePropertyChanged(nameof(IsEmulatorPaused));
                  this.RaisePropertyChanged(nameof(IsEmulatorUninitialized));
                  this.RaisePropertyChanged(nameof(AudioSettingsEnabled));
                  this.RaisePropertyChanged(nameof(StatusEmulatorStateText));
              });

        this.WhenAnyValue(x => x.SelectedSystemName, x => x.SelectedSystemVariant)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(StatusSystemText)));

        _statusFpsTimer = new global::Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _statusFpsTimer.Tick += (_, _) => UpdateStatusFps();
        _statusFpsTimer.Start();

        _scale = _hostApp
            .WhenAnyValue(x => x.Scale)
            .Select(s => (double)s)
            .ToProperty(this, x => x.Scale);

        _validationErrors = _hostApp
            .WhenAnyValue(x => x.ValidationErrors)
            .Select(errors => new ObservableCollection<string>(errors))
            .ToProperty(this, x => x.ValidationErrors);

        _hasValidationErrors = _hostApp
            .WhenAnyValue(x => x.ValidationErrors)
            .Select(errors => errors != null && errors.Count > 0)
            .ToProperty(this, x => x.HasValidationErrors);

        _isStatisticsPanelVisible = _hostApp
            .WhenAnyValue(x => x.IsStatsPanelVisible)
            .ToProperty(this, x => x.IsStatisticsPanelVisible);

        // Subscribe to Monitor.IsVisible changes via HostApp.Monitor
        // When Monitor changes (new system started), resubscribe to the new Monitor's PropertyChanged
        _hostApp
            .WhenAnyValue(x => x.Monitor)
            .Subscribe(monitor =>
            {
                // Unsubscribe from previous monitor if exists
                if (_currentMonitor != null)
                {
                    _currentMonitor.PropertyChanged -= OnMonitorPropertyChanged;
                }

                _currentMonitor = monitor;

                // Update visibility when monitor changes (e.g., on Stop, Monitor becomes null)
                IsMonitorVisible = monitor?.IsVisible ?? false;

                // Subscribe to new monitor's PropertyChanged
                if (monitor != null)
                {
                    monitor.PropertyChanged += OnMonitorPropertyChanged;
                }
            });

        // Initialize Audio properties - track changes to CurrentHostSystemConfig
        _audioSupported = _hostApp
            .WhenAnyValue(x => x.CurrentHostSystemConfig)
            .Select(config => config?.AudioSupported ?? false)
            .ToProperty(this, x => x.AudioSupported);

        // Subscribe to AudioSupported changes AFTER ToProperty to ensure the value is updated first
        this.WhenAnyValue(x => x.AudioSupported)
             .Subscribe(_ =>
             {
                 // Notify AudioSettingsEnabled that depends on AudioSupported
                 this.RaisePropertyChanged(nameof(AudioSettingsEnabled));
             });

        // Subscribe to AudioEnabled changes from HostApp to update the UI property
        _hostApp
            .WhenAnyValue(x => x.CurrentHostSystemConfig)
            .Select(config => config?.SystemConfig?.AudioEnabled ?? false)
            .Subscribe(enabled =>
            {
                if (_audioEnabled != enabled)
                {
                    _audioEnabled = enabled;
                    this.RaisePropertyChanged(nameof(AudioEnabled));
                }
            });

        _audioTooltip = _hostApp
            .WhenAnyValue(x => x.CurrentHostSystemConfig)
            .Select(config => GetAudioToolTip(config))
            .ToProperty(this, x => x.AudioTooltip);

        // External debug server (Desktop only — null on Browser)
        _externalDebugController = App.Current?.ExternalDebugController;
        if (_externalDebugController != null)
        {
            _isExternalDebugListening = _externalDebugController.IsListening;
            _isExternalDebugClientConnected = _externalDebugController.IsClientConnected;
            _externalDebugPort = _externalDebugController.Port;
            _externalDebugPortText = _externalDebugPort.ToString(CultureInfo.InvariantCulture);
            _externalDebugBindAddress = _externalDebugController.BindAddress;
            _externalDebugBindAddressText = _externalDebugBindAddress;
            _externalDebugController.StateChanged += OnExternalDebugControllerStateChanged;
        }

        // Remote control server (Desktop only — null on Browser)
        _remoteControlController = App.Current?.RemoteControlController;
        if (_remoteControlController != null)
        {
            _isRemoteControlListening = _remoteControlController.IsListening;
            _isRemoteClientConnected = _remoteControlController.IsClientConnected;
            if (_remoteControlController.IsListening)
            {
                _remoteControlPort = _remoteControlController.Port;
                _remoteControlBindAddress = _remoteControlController.BindAddress;
            }
            _remoteControlPortText = _remoteControlPort.ToString(CultureInfo.InvariantCulture);
            _remoteControlBindAddressText = _remoteControlBindAddress;
            _remoteControlController.StateChanged += OnRemoteControllerStateChanged;
        }

        ToggleExternalDebugCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                if (_externalDebugController!.IsListening)
                    await _externalDebugController.StopAsync();
                else
                    await _externalDebugController.StartAsync(_externalDebugPort, _externalDebugBindAddress);
            },
            this.WhenAnyValue(
                x => x.IsExternalDebugClientConnected,
                x => x.IsExternalDebugListening,
                x => x.IsExternalDebugPortValid,
                x => x.IsExternalDebugBindAddressValid,
                (connected, listening, isPortValid, isBindAddressValid) => !connected && (listening || (isPortValid && isBindAddressValid))),
            RxSchedulers.MainThreadScheduler);

        ToggleRemoteControlCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                if (_remoteControlController!.IsListening)
                    await _remoteControlController.StopAsync();
                else
                    await _remoteControlController.StartAsync(_remoteControlPort, _remoteControlBindAddress);
            },
            this.WhenAnyValue(
                x => x.IsRemoteControlListening,
                x => x.IsRemoteControlPortValid,
                x => x.IsRemoteControlBindAddressValid,
                (listening, isPortValid, isBindAddressValid) => listening || (isPortValid && isBindAddressValid)),
            RxSchedulers.MainThreadScheduler);

        // Initialize ReactiveCommands for ComboBox selections
        SelectSystemCommand = ReactiveCommandHelper.CreateSafeCommand<string>(
            async (selectedSystem) =>
            {
                if (!string.IsNullOrEmpty(selectedSystem) && _hostApp.SelectedSystemName != selectedSystem)
                {
                    await _hostApp.SelectSystem(selectedSystem);
                }
            },
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state == EmulatorState.Uninitialized),
            RxSchedulers.MainThreadScheduler); // RxSchedulers.MainThreadScheduler required for it working in Browser app

        SelectSystemVariantCommand = ReactiveCommandHelper.CreateSafeCommand<string>(
            async (selectedVariant) =>
            {
                if (!string.IsNullOrEmpty(selectedVariant) && _hostApp.SelectedSystemConfigurationVariant != selectedVariant)
                {
                    await _hostApp.SelectSystemConfigurationVariant(selectedVariant);
                }
            },
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state == EmulatorState.Uninitialized),
            RxSchedulers.MainThreadScheduler); // RxSchedulers.MainThreadScheduler required for it working in Browser app

        // Initialize ReactiveCommands for buttons
        StartCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await _hostApp.Start(),
            this.WhenAnyValue(
                x => x.EmulatorState,
                x => x.HasValidationErrors,
                (state, hasErrors) => !hasErrors && state != EmulatorState.Running),
            RxSchedulers.MainThreadScheduler); // RxSchedulers.MainThreadScheduler required for it working in Browser app

        PauseCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => _hostApp.Pause(),
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state == EmulatorState.Running),
            RxSchedulers.MainThreadScheduler); // RxSchedulers.MainThreadScheduler required for it working in Browser app

        StopCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => _hostApp.Stop(),
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state != EmulatorState.Uninitialized),
            RxSchedulers.MainThreadScheduler); // RxSchedulers.MainThreadScheduler required for it working in Browser app

        ResetCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await _hostApp.Reset(),
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state != EmulatorState.Uninitialized),
            RxSchedulers.MainThreadScheduler); // RxSchedulers.MainThreadScheduler required for it working in Browser app

        MonitorCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => _hostApp.Monitor?.Toggle(),
            this.WhenAnyValue(
                x => x.EmulatorState,
                x => x.IsExternalDebuggerAttached,
                (state, isExternalDebuggerAttached) => state == EmulatorState.Running && !isExternalDebuggerAttached),
            RxSchedulers.MainThreadScheduler); // RxSchedulers.MainThreadScheduler required for it working in Browser app

        StatsCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => _hostApp.ToggleStatisticsPanel(),
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state != EmulatorState.Uninitialized),
            RxSchedulers.MainThreadScheduler); // RxSchedulers.MainThreadScheduler required for it working in Browser app

        ClearLogCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                _logger.LogInformation("Clearing log messages via ClearLogCommand");
                lock (_logUpdateLock)
                {
                    _logMessagesBackingStore.Clear();
                    _logMessages.Clear();
                    _hasPendingLogUpdates = false;
                }
                UpdateLogTabHeader();
                if (_hostApp.LogStore is DotNet6502InMemLogStore logStore)
                {
                    logStore.Clear();
                }
            },
            this.WhenAnyValue(
                x => x.LogMessages.Count,
                count => count > 0),
            RxSchedulers.MainThreadScheduler);

        ToggleScriptEnabledCommand = ReactiveCommandHelper.CreateSafeCommand<string>(
            (fileName) =>
            {
                var entry = _scriptEntries.FirstOrDefault(e => e.FileName == fileName);
                if (entry == null) return;
                bool newEnabled = entry.IsUserDisabled;
                _hostApp.ScriptingEngine.SetScriptEnabled(fileName, newEnabled);
            },
            null,
            RxSchedulers.MainThreadScheduler);

        ReloadScriptCommand = ReactiveCommandHelper.CreateSafeCommand<string>(
            (fileName) =>
            {
                _hostApp.ScriptingEngine.ReloadScript(fileName);
            },
            null,
            RxSchedulers.MainThreadScheduler);

        AddScriptCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                RequestAddScript?.Invoke(this, EventArgs.Empty);
            },
            null,
            RxSchedulers.MainThreadScheduler);

        EditScriptCommand = ReactiveCommandHelper.CreateSafeCommand<string>(
            (fileName) =>
            {
                RequestEditScript?.Invoke(this, fileName);
            },
            null,
            RxSchedulers.MainThreadScheduler);

        DeleteScriptCommand = ReactiveCommandHelper.CreateSafeCommand<string>(
            async (fileName) =>
            {
                var tcs = new TaskCompletionSource<bool>();
                var args = new DeleteScriptConfirmationEventArgs(fileName, tcs);
                RequestDeleteScript?.Invoke(this, args);
                if (await tcs.Task)
                    _hostApp.DeleteScript(fileName);
            },
            null,
            RxSchedulers.MainThreadScheduler);

        RefreshScriptsCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => _hostApp.RefreshScripts(),
            null,
            RxSchedulers.MainThreadScheduler);

        OpenScriptFolderCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => RequestOpenScriptFolder?.Invoke(this, EventArgs.Empty),
            null,
            RxSchedulers.MainThreadScheduler);

        SortByColumnCommand = ReactiveCommandHelper.CreateSafeCommand<ScriptSortColumn>(
            col =>
            {
                if (_scriptSortColumn == col)
                    _scriptSortAscending = !_scriptSortAscending;
                else
                {
                    _scriptSortColumn = col;
                    _scriptSortAscending = true;
                }
                ApplyScriptSort();
                this.RaisePropertyChanged(nameof(FileNameSortIndicator));
                this.RaisePropertyChanged(nameof(StatusSortIndicator));
                this.RaisePropertyChanged(nameof(YieldSortIndicator));
                this.RaisePropertyChanged(nameof(HooksSortIndicator));
            },
            null,
            RxSchedulers.MainThreadScheduler);

        // Emulator Options command - only enabled when emulator is uninitialized
        EmulatorOptionsCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                EmulatorOptionsRequested?.Invoke(this, EventArgs.Empty);
            },
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state == EmulatorState.Uninitialized),
            RxSchedulers.MainThreadScheduler);

        // Initialize timer for batched log UI updates
        InitializeLogUpdateTimer();

        // Populate log messages initially
        RefreshLogMessages();
        // Subscribe to new log messages
        _hostApp.LogStore?.LogMessageAdded += (sender, logEntry) =>
            {
                // Add message to backing store on current thread (no UI dispatch!)
                lock (_logUpdateLock)
                {
                    _logMessagesBackingStore.Add(new LogDisplayEntry(logEntry));
                    _hasPendingLogUpdates = true;
                }

                // Keep the tab header/error badge in sync even while the log tab is hidden and
                // the visible collection is intentionally not updated yet.
                global::Avalonia.Threading.Dispatcher.UIThread.Post(UpdateLogTabHeader);
            };

        // System-specific ViewModel initializations
        this.WhenAnyValue(x => x.SelectedSystemName)
                 .Subscribe(_ =>
                 {
                     this.RaisePropertyChanged(nameof(IsC64SystemSelected));
                     ActiveMenuContributor = ResolveMenuContributor();
                 });

        // Initialize scripts tab data and subscribe to status changes
        CanManageScripts = _hostApp.CanManageScripts;
        CanLoadExamples = _hostApp.CanLoadExamples;
        LoadExamplesCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await _hostApp.LoadExamplesAsync(),
            null,
            RxSchedulers.MainThreadScheduler);
        RefreshScriptStatuses();
        _hostApp.ScriptingEngine.ScriptStatusChanged += OnScriptStatusChanged;
    }

    private string GetAudioToolTip(IHostSystemConfig config)
    {
        if (config == null)
            return string.Empty;

        if (!config.AudioSupported)
            return "Audio is not supported on current platform.";

        if (PlatformDetection.IsRunningOnDesktop())
            return "Audio is experimental. Fast assembly routines may not play audio correctly.";

        if (PlatformDetection.IsRunningInWebAssembly())
            return "Audio is experimental. Fast assembly routines may not play audio correctly. In browser there is a bigger performance cost that may affect entire emulation FPS. See config settings menu for different audio profiles to balance accuracy against latency.";

        return string.Empty;
    }



    public async Task InitializeAsync()
    {
        await SetDefaultSystemSelectionAsync();
    }

    private ISystemMenuContributor? ResolveMenuContributor()
    {
        // Each system's menu ViewModel implements ISystemMenuContributor when it contributes
        // shortcuts. Add more `else if` branches here as new systems gain shortcuts.
        if (IsC64SystemSelected && C64MenuViewModel is ISystemMenuContributor c64Contributor)
            return c64Contributor;
        return null;
    }

    private async Task SetDefaultSystemSelectionAsync()
    {
        if (HostApp == null)
            return;

        // If an automated-startup runner was configured, invoke it instead of the default
        // system selection. Browser uses this for URL-driven automation; Desktop uses it for
        // CLI-driven automation; and the Lua-script case passes a no-op runner to express
        // "skip default selection — the script owns the lifecycle".
        // Running here (from MainView's Loaded event) means the view tree has been laid out
        // and rendered at least once, which the browser frame loop's InvalidateVisual relies on.
        var runner = App.Current?.AutomatedStartupRunner;
        if (runner != null)
        {
            _logger.LogInformation("Running automated startup from MainViewModel.InitializeAsync");
            await runner(HostApp);
            return;
        }

        _logger.LogInformation($"Setting default system '{_emulatorConfig.DefaultEmulator}' during MainViewModel initialization");
        await HostApp.SelectSystem(_emulatorConfig.DefaultEmulator);
    }

    /// <summary>
    /// Initialize the timer for batched log UI updates
    /// </summary>
    private void InitializeLogUpdateTimer()
    {
        _logUpdateTimer = new Timer(500); // Interval in ms
        _logUpdateTimer.Elapsed += OnLogUpdateTimerElapsed;
        _logUpdateTimer.AutoReset = true;
        _logUpdateTimer.Start();
    }

    /// <summary>
    /// Timer callback for batched log UI updates - only updates UI when log tab is visible
    /// </summary>
    private void OnLogUpdateTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        lock (_logUpdateLock)
        {
            // Only update UI notifications if there are pending updates and log tab is visible
            if (_hasPendingLogUpdates && IsLogTabVisible)
            {
                // Copy new messages from backing store to UI collection
                var newMessages = _logMessagesBackingStore.Skip(_logMessages.Count).ToList();
                _hasPendingLogUpdates = false;

                // Dispatch UI update to UI thread
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        // Add new messages to UI collection
                        foreach (var message in newMessages)
                        {
                            _logMessages.Add(message);
                        }
                        UpdateLogTabHeader();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating log UI");
                    }
                });
            }
            // If log tab is not visible, keep pending updates flag to update later when tab becomes visible
            else if (_hasPendingLogUpdates && !IsLogTabVisible)
            {
                // Keep the flag, will update when tab becomes visible
            }
            else
            {
                // Reset flag if no pending updates
                _hasPendingLogUpdates = false;
            }
        }
    }

    /// <summary>
    /// Refresh the log messages from the HostApp's log store
    /// </summary>
    public void RefreshLogMessages()
    {
        if (_hostApp?.LogStore == null)
            return;
        var logs = _hostApp.LogStore.GetFullLogMessages();

        lock (_logUpdateLock)
        {
            // Update both backing store and UI collection with initial data
            _logMessagesBackingStore.Clear();
            foreach (var log in logs)
            {
                _logMessagesBackingStore.Add(new LogDisplayEntry(log));
            }

            // Only update UI collection if the logs have changed
            if (logs.Count != _logMessages.Count || !logs.Select(l => l.Message).SequenceEqual(_logMessages.Select(l => l.Message)))
            {
                _logMessages.Clear();
                foreach (var entry in _logMessagesBackingStore)
                {
                    _logMessages.Add(entry);
                }
            }
        }
        UpdateLogTabHeader();
    }

    /// <summary>
    /// Updates the log tab header and error state based on error/critical message count
    /// </summary>
    private void UpdateLogTabHeader()
    {
        int errorCount;
        lock (_logUpdateLock)
        {
            errorCount = CountLogErrors(_logMessagesBackingStore);
        }
        UpdateLogTabHeader(errorCount);
    }

    private void UpdateLogTabHeader(int errorCount)
    {
        LogTabHeader = errorCount > 0 ? $"Log ({errorCount})" : "Log";
        HasLogErrors = errorCount > 0;
    }

    private static int CountLogErrors(IEnumerable<LogDisplayEntry> logEntries)
    {
        return logEntries.Count(m => m.LogLevel == LogLevel.Error || m.LogLevel == LogLevel.Critical);
    }

    private void RefreshScriptStatuses()
    {
        var engine = _hostApp.ScriptingEngine;
        IsScriptingEnabled = engine.IsEnabled;

        var statuses = engine.GetScriptStatuses();
        _scriptEntries.Clear();
        foreach (var status in statuses)
            _scriptEntries.Add(new ScriptDisplayEntry(status));

        ApplyScriptSort();
        UpdateScriptsTabHeader();
    }

    private void ApplyScriptSort()
    {
        var sorted = (_scriptSortColumn switch
        {
            ScriptSortColumn.Status   => _scriptSortAscending ? _scriptEntries.OrderBy(e => e.Status)    : _scriptEntries.OrderByDescending(e => e.Status),
            ScriptSortColumn.YieldType => _scriptSortAscending ? _scriptEntries.OrderBy(e => e.YieldType) : _scriptEntries.OrderByDescending(e => e.YieldType),
            ScriptSortColumn.Hooks    => _scriptSortAscending ? _scriptEntries.OrderBy(e => e.Hooks)     : _scriptEntries.OrderByDescending(e => e.Hooks),
            _                         => _scriptSortAscending ? _scriptEntries.OrderBy(e => e.FileName)  : _scriptEntries.OrderByDescending(e => e.FileName),
        }).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            int current = _scriptEntries.IndexOf(sorted[i]);
            if (current != i)
                _scriptEntries.Move(current, i);
        }
    }

    private void UpdateScriptsTabHeader()
    {
        var systemDisabledCount = _scriptEntries.Count(s => s.IsDisabled);
        var activeCount = _scriptEntries.Count(s => s.IsScriptEnabled);

        if (_scriptEntries.Count == 0)
            ScriptsTabHeader = "Scripts";
        else if (systemDisabledCount > 0 && activeCount > 0)
            ScriptsTabHeader = $"Scripts ({activeCount}, {systemDisabledCount} disabled)";
        else if (systemDisabledCount > 0)
            ScriptsTabHeader = $"Scripts ({systemDisabledCount} disabled)";
        else if (activeCount > 0)
            ScriptsTabHeader = $"Scripts ({activeCount})";
        else
            ScriptsTabHeader = "Scripts";

        // Only flag red styling for system-disabled scripts (errors), not user-disabled
        HasDisabledScripts = systemDisabledCount > 0;
    }

    private void OnScriptStatusChanged(object? sender, EventArgs e)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => RefreshScriptStatuses());
    }

    /// <summary>
    /// Creates a MonitorViewModel for the current monitor instance.
    /// This method should be called by views that need to display the monitor.
    /// </summary>
    /// <returns>MonitorViewModel if monitor is available, null otherwise</returns>
    private MonitorViewModel? CreateMonitorViewModel()
    {
        if (_hostApp.Monitor == null)
            return null;

        return new MonitorViewModel(_hostApp.Monitor, _hostApp);
    }

    /// <summary>
    /// Handles StateChanged events from the external debug controller.
    /// Fired from a background thread; dispatches property notifications to the UI thread.
    /// </summary>
    private void OnExternalDebugControllerStateChanged(object? sender, EventArgs e)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsExternalDebugListening = _externalDebugController?.IsListening ?? false;
            IsExternalDebugClientConnected = _externalDebugController?.IsClientConnected ?? false;
            if (_externalDebugController != null)
            {
                ExternalDebugPortText = _externalDebugController.Port.ToString(CultureInfo.InvariantCulture);
                ExternalDebugBindAddressText = _externalDebugController.BindAddress;
            }
            this.RaisePropertyChanged(nameof(ExternalDebugStatusText));
            this.RaisePropertyChanged(nameof(ExternalDebugToggleButtonText));
        });
    }

    private void OnRemoteControllerStateChanged(object? sender, EventArgs e)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsRemoteControlListening = _remoteControlController?.IsListening ?? false;
            bool isRemoteClientConnected = _remoteControlController?.IsClientConnected ?? false;
            IsRemoteClientConnected = isRemoteClientConnected;
            if (_remoteControlController != null)
            {
                RemoteControlPortText = _remoteControlController.Port.ToString(CultureInfo.InvariantCulture);
                RemoteControlBindAddressText = _remoteControlController.BindAddress;
            }
            this.RaisePropertyChanged(nameof(RemoteControlStatusText));
            this.RaisePropertyChanged(nameof(RemoteControlToggleButtonText));
        });
    }

    private static string FilterDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string FilterIpv4Characters(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return new string(value.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
    }

    private static bool TryParsePortText(string? value, out int port)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port))
            return false;

        return port >= 1 && port <= 65535;
    }

    private static bool IsValidIpv4Address(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('.');
        if (parts.Length != 4)
            return false;

        foreach (var part in parts)
        {
            if (part.Length is < 1 or > 3)
                return false;

            if (!part.All(char.IsDigit))
                return false;

            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var octet))
                return false;

            if (octet < 0 || octet > 255)
                return false;
        }

        return true;
    }

    private void SetPortText(
        string? value,
        ref string field,
        string textPropertyName,
        string isValidPropertyName,
        string validationMessagePropertyName,
        string toolTipPropertyName,
        Action<int> applyValidValue)
    {
        var rawValue = value ?? string.Empty;
        var filteredValue = FilterDigits(rawValue);
        var acceptedValue = filteredValue;

        if (rawValue.Length == 0)
        {
            acceptedValue = string.Empty;
        }
        else if (filteredValue.Length == 0 || filteredValue.Length > 5 || !TryParsePortText(filteredValue, out var parsedPort))
        {
            acceptedValue = field;
        }
        else
        {
            applyValidValue(parsedPort);
        }

        UpdateFilteredText(value, acceptedValue, ref field, textPropertyName);
        RaiseInputValidationProperties(isValidPropertyName, validationMessagePropertyName, toolTipPropertyName);
    }

    private void SetIpv4Text(
        string? value,
        ref string field,
        string textPropertyName,
        string isValidPropertyName,
        string validationMessagePropertyName,
        string toolTipPropertyName,
        Action<string> applyValidValue)
    {
        var filteredValue = FilterIpv4Characters(value);
        UpdateFilteredText(value, filteredValue, ref field, textPropertyName);

        if (IsValidIpv4Address(filteredValue))
        {
            applyValidValue(filteredValue);
        }

        RaiseInputValidationProperties(isValidPropertyName, validationMessagePropertyName, toolTipPropertyName);
    }

    private void UpdateFilteredText(string? rawValue, string filteredValue, ref string field, string textPropertyName)
    {
        if (!string.Equals(field, filteredValue, StringComparison.Ordinal))
        {
            field = filteredValue;
            this.RaisePropertyChanged(textPropertyName);
            return;
        }

        if (!string.Equals(rawValue ?? string.Empty, filteredValue, StringComparison.Ordinal))
        {
            this.RaisePropertyChanged(textPropertyName);
        }
    }

    private void RaiseInputValidationProperties(string isValidPropertyName, string validationMessagePropertyName, string toolTipPropertyName)
    {
        this.RaisePropertyChanged(isValidPropertyName);
        this.RaisePropertyChanged(validationMessagePropertyName);
        this.RaisePropertyChanged(toolTipPropertyName);
    }

    private void UpdateStatusFps()
    {
        if (EmulatorState != EmulatorState.Running)
        {
            StatusFpsText = string.Empty;
            return;
        }

        try
        {
            var fpsStat = _hostApp.GetStats()
                .FirstOrDefault(s => s.name.EndsWith("OnUpdateFPS", StringComparison.OrdinalIgnoreCase));
            if (fpsStat.stat is Highbyte.DotNet6502.Systems.Instrumentation.Stats.PerSecondTimedStat perSecond && perSecond.Value.HasValue)
                StatusFpsText = $"{Math.Round(perSecond.Value.Value)} fps";
            else
                StatusFpsText = string.Empty;
        }
        catch
        {
            StatusFpsText = string.Empty;
        }
    }

    private async Task UpdateRemoteClientIndicatorAsync(bool isConnected)
    {
        if (_remoteClientIndicatorCts != null)
        {
            await _remoteClientIndicatorCts.CancelAsync();
            _remoteClientIndicatorCts.Dispose();
        }

        var cts = new System.Threading.CancellationTokenSource();
        _remoteClientIndicatorCts = cts;
        var cancellationToken = cts.Token;

        try
        {
            if (isConnected)
            {
                _remoteClientIndicatorShownAtUtc = DateTimeOffset.UtcNow;
                RemoteClientIndicatorOpacity = 1.0;
                return;
            }

            if (_remoteClientIndicatorShownAtUtc.HasValue)
            {
                var elapsed = DateTimeOffset.UtcNow - _remoteClientIndicatorShownAtUtc.Value;
                var remaining = RemoteClientIndicatorMinimumVisibleDuration - elapsed;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, cancellationToken);
            }

            RemoteClientIndicatorOpacity = 0.0;
            _remoteClientIndicatorShownAtUtc = null;
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Handles PropertyChanged events from the AvaloniaMonitor.
    /// Updates IsMonitorVisible which will trigger MainView to show/hide the monitor UI.
    /// </summary>
    private void OnMonitorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AvaloniaMonitor.IsVisible) && _currentMonitor != null)
        {
            IsMonitorVisible = _currentMonitor.IsVisible;
        }
    }

    /// <summary>
    /// Dispose of resources including the log update timer
    /// </summary>
    public void Dispose()
    {
        // Unsubscribe from scripting engine events
        _hostApp.ScriptingEngine.ScriptStatusChanged -= OnScriptStatusChanged;

        // Unsubscribe from external debug controller events
        if (_externalDebugController != null)
            _externalDebugController.StateChanged -= OnExternalDebugControllerStateChanged;

        // Unsubscribe from remote control controller events
        if (_remoteControlController != null)
            _remoteControlController.StateChanged -= OnRemoteControllerStateChanged;

        _remoteClientIndicatorCts?.Cancel();
        _remoteClientIndicatorCts?.Dispose();
        _remoteClientIndicatorCts = null;

        if (_statusFpsTimer != null)
        {
            _statusFpsTimer.Stop();
            _statusFpsTimer = null;
        }

        // Unsubscribe from monitor events
        if (_currentMonitor != null)
        {
            _currentMonitor.PropertyChanged -= OnMonitorPropertyChanged;
            _currentMonitor = null;
        }

        if (_logUpdateTimer != null)
        {
            _logUpdateTimer.Stop();
            _logUpdateTimer.Elapsed -= OnLogUpdateTimerElapsed;
            _logUpdateTimer.Dispose();
            _logUpdateTimer = null;
        }
    }
}

/// <summary>
/// Wrapper class for displaying log entries with appropriate symbols/icons
/// </summary>
public class LogDisplayEntry
{
    public string Symbol { get; }
    public string Message { get; }
    public LogLevel LogLevel { get; }
    public string FormattedDisplay { get; }

    // Boolean properties for conditional class binding
    public bool IsTrace { get; }
    public bool IsDebug { get; }
    public bool IsInfo { get; }
    public bool IsWarning { get; }
    public bool IsError { get; }
    public bool IsCritical { get; }

    public LogDisplayEntry(LogEntry logEntry)
    {
        LogLevel = logEntry.LogLevel;
        Message = logEntry.Message.TrimEnd();
        Symbol = GetSymbolForLogLevel(logEntry.LogLevel);
        FormattedDisplay = $"{Symbol} {Message}";

        // Set boolean flags for conditional class binding
        IsTrace = logEntry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Trace;
        IsDebug = logEntry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Debug;
        IsInfo = logEntry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Information;
        IsWarning = logEntry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Warning;
        IsError = logEntry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Error;
        IsCritical = logEntry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Critical;
    }

    private static string GetSymbolForLogLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "○",        // Hollow circle for trace (can be colored)
            LogLevel.Debug => "●",        // Filled circle for debug (can be colored)
            LogLevel.Information => "●",  // Filled circle for info (can be colored)
            LogLevel.Warning => "●",      // Filled circle for warning (can be colored)
            LogLevel.Error => "●",        // Filled circle for error (can be colored)
            LogLevel.Critical => "❌",    // Red cross mark for critical (kept as requested)
            LogLevel.None => "○",         // Hollow circle for general/none (can be colored)
            _ => "?"                      // Question mark for unknown
        };
    }
}

/// <summary>
/// Display wrapper for a single script entry in the Scripts tab.
/// </summary>
public class ScriptDisplayEntry
{
    public string FileName { get; }
    public string Status { get; }
    public string YieldType { get; }
    public string Hooks { get; }

    public bool IsDisabled { get; }
    public bool IsRunning { get; }
    public bool IsCompleted { get; }
    public bool IsHookOnly { get; }
    public bool IsUserDisabled { get; }
    public bool CanToggle { get; }
    public bool CanReload { get; }
    public bool IsScriptEnabled { get; }

    public ScriptDisplayEntry(ScriptStatus scriptStatus)
    {
        FileName = scriptStatus.FileName;

        IsRunning = scriptStatus.State == ScriptExecutionState.Running;
        IsDisabled = scriptStatus.State == ScriptExecutionState.Disabled;
        IsUserDisabled = scriptStatus.State == ScriptExecutionState.UserDisabled;
        IsCompleted = scriptStatus.State == ScriptExecutionState.Completed;
        IsHookOnly = scriptStatus.State == ScriptExecutionState.HookOnly;
        CanToggle = scriptStatus.CanToggle;
        CanReload = scriptStatus.CanReload;
        IsScriptEnabled = !IsUserDisabled && !IsDisabled;

        Status = scriptStatus.State switch
        {
            ScriptExecutionState.Running => "Running",
            ScriptExecutionState.Disabled => "Disabled",
            ScriptExecutionState.UserDisabled => "Disabled (user)",
            ScriptExecutionState.Completed => "Completed",
            ScriptExecutionState.HookOnly => "Hook-only",
            _ => "Unknown"
        };

        YieldType = scriptStatus.YieldType switch
        {
            ScriptYieldType.FrameAdvance => "FrameAdvance",
            ScriptYieldType.Tick => "Tick",
            _ => "-"
        };

        Hooks = scriptStatus.Hooks.Count > 0
            ? string.Join(", ", scriptStatus.Hooks)
            : "-";
    }
}

public enum ScriptSortColumn { FileName, Status, YieldType, Hooks }

public class DeleteScriptConfirmationEventArgs : EventArgs
{
    public string FileName { get; }
    private readonly TaskCompletionSource<bool> _tcs;

    public DeleteScriptConfirmationEventArgs(string fileName, TaskCompletionSource<bool> tcs)
    {
        FileName = fileName;
        _tcs = tcs;
    }

    public void SetResult(bool confirmed) => _tcs.TrySetResult(confirmed);
}
