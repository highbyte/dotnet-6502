using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly ILogger<MainViewModel> _logger;

    // Expose HostApp for child views/viewmodels that need it
    public AvaloniaHostApp HostApp => _hostApp;

    // Child ViewModels exposed as properties for XAML binding
    public C64MenuViewModel C64MenuViewModel { get; }
    public StatisticsViewModel StatisticsViewModel { get; }

    // Constructor with dependency injection - child ViewModels injected!
    public MainViewModel(
        AvaloniaHostApp hostApp,
        C64MenuViewModel c64MenuViewModel,  // Injected by DI with AvaloniaHostApp
        StatisticsViewModel statisticsViewModel,
        ILoggerFactory loggerFactory)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _logger = loggerFactory?.CreateLogger<MainViewModel>() ?? throw new ArgumentNullException(nameof(loggerFactory));

        // Store injected child ViewModels
        C64MenuViewModel = c64MenuViewModel ?? throw new ArgumentNullException(nameof(c64MenuViewModel));
        StatisticsViewModel = statisticsViewModel ?? throw new ArgumentNullException(nameof(statisticsViewModel));
        InitializeAvailableSystems();
    }

    // NO Init() method - everything happens in constructor!

    public string SelectedSystemName => _hostApp.SelectedSystemName;

    public string SelectedSystemVariant => _hostApp.SelectedSystemConfigurationVariant;

    public ObservableCollection<string> AvailableSystemVariants { get; } = new();

    public EmulatorState EmulatorState
    {
        get => _hostApp?.EmulatorState ?? EmulatorState.Uninitialized;
    }

    public bool IsEmulatorNotRunning => EmulatorState != EmulatorState.Running;

    public bool IsEmulatorRunning => EmulatorState == EmulatorState.Running;

    public bool IsC64SystemSelected => string.Equals(SelectedSystemName, C64.SystemName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Generic method to check if a specific system is selected
    /// </summary>
    /// <param name="systemName">The system name to check (e.g., "C64")</param>
    /// <returns>True if the specified system is selected</returns>
    public bool IsSystemSelected(string systemName)
    {
        return string.Equals(SelectedSystemName, systemName, StringComparison.OrdinalIgnoreCase);
    }

    // UI Control Enabled Properties based on EmulatorState
    public bool IsSystemSelectionEnabled => EmulatorState == EmulatorState.Uninitialized;

    public bool IsVariantSelectionEnabled => EmulatorState == EmulatorState.Uninitialized;

    public bool IsStartButtonEnabled => _isSystemConfigValid && EmulatorState != EmulatorState.Running;

    public bool IsPauseButtonEnabled => EmulatorState == EmulatorState.Running;

    public bool IsStopButtonEnabled => EmulatorState != EmulatorState.Uninitialized;

    public bool IsResetButtonEnabled => EmulatorState != EmulatorState.Uninitialized;
    public bool IsMonitorButtonEnabled => EmulatorState != EmulatorState.Uninitialized;
    public bool IsStatsButtonEnabled => EmulatorState != EmulatorState.Uninitialized;

    // Private field to cache system config validity - updated when system changes
    private bool _isSystemConfigValid = false;

    // Private field to cache validation errors
    private readonly ObservableCollection<string> _validationErrors = new();
    public ObservableCollection<string> ValidationErrors => _validationErrors;

    public bool HasValidationErrors => _validationErrors.Count > 0;

    // Statistics panel visibility
    private bool _isStatisticsPanelVisible = false;
    public bool IsStatisticsPanelVisible
    {
        get => _isStatisticsPanelVisible;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isStatisticsPanelVisible, value))
            {
                this.RaisePropertyChanged(nameof(StatisticsPanelColumnWidth));
            }
        }
    }

    // Statistics panel column width - bind this to the grid column width
    //public string StatisticsPanelColumnWidth => IsStatisticsPanelVisible ? "Auto" : "0";
    public string StatisticsPanelColumnWidth => "250";

    public ObservableCollection<string> AvailableSystems { get; } = new();

    /// <summary>
    /// Called when system selection has completed (especially important in WebAssembly where SelectSystem is async)
    /// </summary>
    public void OnSystemSelectionCompleted()
    {
        // Update available variants based on the newly selected system
        UpdateAvailableVariants();

        // Force a full UI refresh to update all bindings
        NotifyEmulatorStateChanged();
    }

    private void NotifyEmulatorStateChanged()
    {
        try
        {
            // Update system config validity
            UpdateSystemConfigValidity();

            this.RaisePropertyChanged(nameof(SelectedSystemName));
            this.RaisePropertyChanged(nameof(SelectedSystemVariant));
            this.RaisePropertyChanged(nameof(EmulatorState));
            this.RaisePropertyChanged(nameof(IsEmulatorNotRunning));
            this.RaisePropertyChanged(nameof(IsEmulatorRunning));
            this.RaisePropertyChanged(nameof(IsC64SystemSelected));

            // Notify UI control enabled state changes
            this.RaisePropertyChanged(nameof(IsSystemSelectionEnabled));
            this.RaisePropertyChanged(nameof(IsVariantSelectionEnabled));
            this.RaisePropertyChanged(nameof(IsStartButtonEnabled));
            this.RaisePropertyChanged(nameof(IsPauseButtonEnabled));
            this.RaisePropertyChanged(nameof(IsStopButtonEnabled));
            this.RaisePropertyChanged(nameof(IsResetButtonEnabled));
            this.RaisePropertyChanged(nameof(IsMonitorButtonEnabled));
            this.RaisePropertyChanged(nameof(IsStatsButtonEnabled));

            // Notify C64-specific property changes
            C64MenuViewModel?.NotifyEmulatorStateChanged();
        }
        catch (Exception ex)
        {
            // Safe error handling for WebAssembly/AOT environments
            try
            {
                System.Console.WriteLine($"Error in NotifyEmulatorStateChanged: {ex?.Message ?? "Unknown error"}");
            }
            catch
            {
                System.Console.WriteLine("Error in NotifyEmulatorStateChanged: Unable to access exception details");
            }
        }
    }

    private void UpdateSystemConfigValidity()
    {
        if (_hostApp != null)
        {
            // Use ConfigureAwait(false) to avoid deadlock and run async operation in background
            _ = Task.Run(async () =>
            {
                try
                {
                    var (isValid, validationErrors) = await _hostApp.IsValidConfigWithDetails();

                    var hasChanged = _isSystemConfigValid != isValid ||
                                     !_validationErrors.SequenceEqual(validationErrors);

                    if (hasChanged)
                    {
                        _isSystemConfigValid = isValid;

                        // Update validation errors on UI thread
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            _validationErrors.Clear();
                            foreach (var error in validationErrors)
                            {
                                _validationErrors.Add(error);
                            }

                            this.RaisePropertyChanged(nameof(IsStartButtonEnabled));
                            this.RaisePropertyChanged(nameof(HasValidationErrors));
                        });
                    }
                }
                catch
                {
                    // If we can't determine validity, assume invalid
                    var hasChanged = _isSystemConfigValid || _validationErrors.Count > 0;

                    if (hasChanged)
                    {
                        _isSystemConfigValid = false;

                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            _validationErrors.Clear();
                            _validationErrors.Add("Unable to validate system configuration");

                            this.RaisePropertyChanged(nameof(IsStartButtonEnabled));
                            this.RaisePropertyChanged(nameof(HasValidationErrors));
                        });
                    }
                }
            });
        }
        else
        {
            _isSystemConfigValid = false;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _validationErrors.Clear();
                this.RaisePropertyChanged(nameof(HasValidationErrors));
            });
        }
    }

    private void InitializeAvailableSystems()
    {
        AvailableSystems.Clear();

        if (_hostApp?.SystemList != null)
        {
            foreach (var systemName in _hostApp.SystemList.Systems)
            {
                AvailableSystems.Add(systemName);
            }

            if (AvailableSystems.Any())
            {
                // SelectedSystemName is now read directly from App.HostApp.SelectedSystemName
                // Update available variants for the selected system
                UpdateAvailableVariants();
                // Notify UI that the selected system name may have changed
                NotifyEmulatorStateChanged();
            }
        }
        else
        {
            AvailableSystems.Add("Loading...");
        }
    }

    private void UpdateAvailableVariants()
    {
        AvailableSystemVariants.Clear();

        if (_hostApp?.AllSelectedSystemConfigurationVariants != null)
        {
            foreach (var variant in _hostApp.AllSelectedSystemConfigurationVariants)
            {
                AvailableSystemVariants.Add(variant);
            }
        }
    }

    /// <summary>
    /// Toggle the visibility of the statistics panel
    /// </summary>
    public void ToggleStatisticsPanel()
    {
        IsStatisticsPanelVisible = !IsStatisticsPanelVisible;
    }

    public void ForceStateRefresh()
    {
        UpdateSystemConfigValidity();
        NotifyEmulatorStateChanged();
    }

    /// <summary>
    /// Notify that the disk image state has changed (attached/detached)
    /// </summary>
    public void NotifyDiskImageStateChanged()
    {
        C64MenuViewModel.NotifyDiskImageStateChanged();
    }
}
