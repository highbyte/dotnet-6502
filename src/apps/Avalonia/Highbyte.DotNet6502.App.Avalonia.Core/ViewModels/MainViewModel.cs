using System;
using System.Collections.ObjectModel;
using System.Linq;
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

    // Expose HostApp for child views/viewmodels that need it
    public AvaloniaHostApp HostApp => _hostApp;

    // Child ViewModels exposed as properties for XAML binding
    public C64MenuViewModel C64MenuViewModel { get; }
    public StatisticsViewModel StatisticsViewModel { get; }
    public EmulatorViewModel EmulatorViewModel { get; }
    public EmulatorPlaceholderViewModel EmulatorPlaceholderViewModel { get; }

    // --- Start Binding Properties ---

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

    public EmulatorStateFlags EmulatorStateFlags { get; }

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

    // Private field to cache validation errors
    private readonly ObservableAsPropertyHelper<ObservableCollection<string>> _validationErrors;
    public ObservableCollection<string> ValidationErrors => _validationErrors.Value;

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

    // --- End Binding Properties ---

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

        EmulatorStateFlags = new EmulatorStateFlags(_hostApp.EmulatorState);

        // Set up reactive properties
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
            .Do(state =>
            {
                EmulatorStateFlags.EmulatorState = state;
            })
            .ToProperty(this, x => x.EmulatorState);

        _scale = _hostApp
            .WhenAnyValue(x => x.Scale)
            .Select(s => (double)s)
            .ToProperty(this, x => x.Scale);

        _validationErrors = _hostApp
            .WhenAnyValue(x => x.ValidationErrors)
            .Select(errors => new ObservableCollection<string>(errors))
            .Do(errors =>
            {
                EmulatorStateFlags.IsSystemConfigValid = errors.Count == 0; ;
            })
            .ToProperty(this, x => x.ValidationErrors);

        _isStatisticsPanelVisible = _hostApp
            .WhenAnyValue(x => x.IsStatsPanelVisible)
            .ToProperty(this, x => x.IsStatisticsPanelVisible);

        _isMonitorVisible = _hostApp
            .WhenAnyValue(x => x.IsMonitorVisible)
            .ToProperty(this, x => x.IsMonitorVisible);

        // Populate log messages initially
        RefreshLogMessages();
        // Subscribe to new log messages
        if (_hostApp.LogStore != null)
        {
            _hostApp.LogStore.LogMessageAdded += (sender, logMessage) =>
            {
                // Always add at end for UI order
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _logMessages.Add(logMessage);
                });
            };
        }

        // System-specific ViewModel initializations
        this.WhenAnyValue(x => x.SelectedSystemName)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsC64SystemSelected)));
    }

    private void NotifySystemSpecificBindings()
    {
        this.RaisePropertyChanged(nameof(IsC64SystemSelected));
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
}
