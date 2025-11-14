using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System.Runtime.InteropServices;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly ILogger<MainViewModel> _logger;

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

    // Computed properties for control enabled states based on EmulatorState
    public bool IsEmulatorRunning => EmulatorState == EmulatorState.Running;
    public bool IsEmulatorUninitialzied => EmulatorState == EmulatorState.Uninitialized;

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

    // Monitor visibility
    private readonly ObservableAsPropertyHelper<bool> _isMonitorVisible;
    public bool IsMonitorVisible => _isMonitorVisible.Value;

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

    private readonly ObservableAsPropertyHelper<bool> _audioEnabled;
    public bool AudioEnabled => _audioEnabled.Value;
    public void ClearMonitorViewModel()
    {
        MonitorViewModel = null;
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
        _logger = loggerFactory?.CreateLogger<MainViewModel>() ?? throw new ArgumentNullException(nameof(loggerFactory));

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

        // Subscribe to EmulatorState changes AFTER ToProperty to ensure the value is updated first
        this.WhenAnyValue(x => x.EmulatorState)
             .Subscribe(_ =>
              {
                  // Notify all computed properties that depend on EmulatorState
                  this.RaisePropertyChanged(nameof(IsEmulatorRunning));
                  this.RaisePropertyChanged(nameof(IsEmulatorUninitialzied));
              });

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

        _isMonitorVisible = _hostApp
            .WhenAnyValue(x => x.IsMonitorVisible)
            .ToProperty(this, x => x.IsMonitorVisible);

        // Initialize Audio properties - track changes to CurrentHostSystemConfig
        _audioSupported = _hostApp
            .WhenAnyValue(x => x.CurrentHostSystemConfig)
            .Select(config => config?.AudioSupported ?? false)
            .ToProperty(this, x => x.AudioSupported);

        _audioEnabled = _hostApp
            .WhenAnyValue(x => x.CurrentHostSystemConfig)
            .Select(config => config?.SystemConfig?.AudioEnabled ?? false)
            .ToProperty(this, x => x.AudioEnabled);

        // Initialize ReactiveCommands for ComboBox selections
        SelectSystemCommand = ReactiveCommand.CreateFromTask<string>(
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
            RxApp.MainThreadScheduler); // RxApp.MainThreadScheduler required for it working in Browser app

        SelectSystemVariantCommand = ReactiveCommand.CreateFromTask<string>(
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
                RxApp.MainThreadScheduler); // RxApp.MainThreadScheduler required for it working in Browser app

        // Initialize ReactiveCommands for buttons
        StartCommand = ReactiveCommand.CreateFromTask(
            async () => await _hostApp.Start(),
            this.WhenAnyValue(
                x => x.EmulatorState,
                x => x.HasValidationErrors,
                (state, hasErrors) => !hasErrors && state != EmulatorState.Running),
            RxApp.MainThreadScheduler); // RxApp.MainThreadScheduler required for it working in Browser app

        PauseCommand = ReactiveCommand.CreateFromTask(
            () =>
            {
                _hostApp.Pause();
                return Task.CompletedTask;
            },
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state == EmulatorState.Running),
            RxApp.MainThreadScheduler); // RxApp.MainThreadScheduler required for it working in Browser app

        StopCommand = ReactiveCommand.CreateFromTask(
            () =>
            {
                _hostApp.Stop();
                return Task.CompletedTask;
            },
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state != EmulatorState.Uninitialized),
            RxApp.MainThreadScheduler); // RxApp.MainThreadScheduler required for it working in Browser app

        ResetCommand = ReactiveCommand.CreateFromTask(
            async () => await _hostApp.Reset(),
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state != EmulatorState.Uninitialized),
            RxApp.MainThreadScheduler); // RxApp.MainThreadScheduler required for it working in Browser app

        MonitorCommand = ReactiveCommand.CreateFromTask(
            () =>
            {
                _hostApp.ToggleMonitor();
                return Task.CompletedTask;
            },
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state != EmulatorState.Uninitialized),
            RxApp.MainThreadScheduler); // RxApp.MainThreadScheduler required for it working in Browser app

        StatsCommand = ReactiveCommand.CreateFromTask(
            () =>
            {
                _hostApp.ToggleStatisticsPanel();
                return Task.CompletedTask;
            },
            this.WhenAnyValue(
                x => x.EmulatorState,
                state => state != EmulatorState.Uninitialized),
            RxApp.MainThreadScheduler); // RxApp.MainThreadScheduler required for it working in Browser app

        ClearLogCommand = ReactiveCommand.Create(
            () =>
            {
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
            RxApp.MainThreadScheduler);

        // Handle command exceptions
        SelectSystemCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error selecting system"));
        SelectSystemVariantCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error selecting system variant"));
        StartCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Start command"));
        PauseCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Pause command"));
        StopCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Stop command"));
        ResetCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Reset command"));
        MonitorCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Monitor command"));
        StatsCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Stats command"));

        // Initialize timer for batched log UI updates
        InitializeLogUpdateTimer();

        // Populate log messages initially
        RefreshLogMessages();
        // Subscribe to new log messages
        if (_hostApp.LogStore != null)
        {
            _hostApp.LogStore.LogMessageAdded += (sender, logEntry) =>
            {
                // Add message to backing store on current thread (no UI dispatch!)
                lock (_logUpdateLock)
                {
                    _logMessagesBackingStore.Add(new LogDisplayEntry(logEntry));
                    _hasPendingLogUpdates = true;
                }
            };
        }

        // System-specific ViewModel initializations
        this.WhenAnyValue(x => x.SelectedSystemName)
                 .Subscribe(_ => this.RaisePropertyChanged(nameof(IsC64SystemSelected)));
    }

    public async Task InitializeAsync()
    {
        await SetDefaultSystemSelection();
    }

    private async Task SetDefaultSystemSelection()
    {
        if (HostApp == null)
            return;

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
        var errorCount = _logMessages.Count(m => m.LogLevel == LogLevel.Error || m.LogLevel == LogLevel.Critical);
        LogTabHeader = errorCount > 0 ? $"Log ({errorCount})" : "Log";
        HasLogErrors = errorCount > 0;
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

        var viewModel = new MonitorViewModel(_hostApp.Monitor);

        // Subscribe to the ViewModel's CloseRequested event to disable monitor when requested
        viewModel.CloseRequested += (sender, e) => _hostApp.DisableMonitor();

        return viewModel;
    }

    /// <summary>
    /// Dispose of resources including the log update timer
    /// </summary>
    public void Dispose()
    {
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
        Message = logEntry.Message;
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
