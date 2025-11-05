using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly ILogger<MainViewModel> _logger;

    // Expose HostApp for EmulatorView that currently needs it (TODO: Consider removing this dependency via MainViewModel. Better that EmulatorViewModel provides it.)
    public AvaloniaHostApp HostApp => _hostApp;

    // Child ViewModels exposed as properties for XAML binding
    public C64MenuViewModel C64MenuViewModel { get; }
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
    private readonly ObservableCollection<string> _logMessages = new();
    public ObservableCollection<string> LogMessages => _logMessages;


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
    public ReactiveCommand<string, Unit> SelectSystemCommand { get; }
    public ReactiveCommand<string, Unit> SelectSystemVariantCommand { get; }
    // --- End ReactiveUI Commands ---

    // Constructor with dependency injection - child ViewModels injected!
    public MainViewModel(
        AvaloniaHostApp hostApp,
        EmulatorConfig emulatorConfig,
        C64MenuViewModel c64MenuViewModel,  // Injected by DI with AvaloniaHostApp
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

        // Handle command exceptions
        SelectSystemCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error selecting system"));
        SelectSystemVariantCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error selecting system variant"));
        StartCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Start command"));
        PauseCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Pause command"));
        StopCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Stop command"));
        ResetCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Reset command"));
        MonitorCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Monitor command"));
        StatsCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Error executing Stats command"));

        // Populate log messages initially
        RefreshLogMessages();
        // Subscribe to new log messages
        if (_hostApp.LogStore != null)
        {
            _hostApp.LogStore.LogMessageAdded += (sender, logEntry) =>
            {
                // Always add at end for UI order
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _logMessages.Add(logEntry.Message);
                });
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
    /// Refresh the log messages from the HostApp's log store
    /// </summary>
    public void RefreshLogMessages()
    {
        if (_hostApp?.LogStore == null)
            return;
        var logs = _hostApp.LogStore.GetLogMessages();
        // Only update if the logs have changed
        if (logs.Count != _logMessages.Count || !logs.SequenceEqual(_logMessages))
        {
            _logMessages.Clear();
            foreach (var log in logs)
            {
                _logMessages.Add(log);
            }
        }
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
}
