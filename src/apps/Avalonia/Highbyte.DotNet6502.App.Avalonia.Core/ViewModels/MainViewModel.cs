using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class MainViewModel : ViewModelBase
{
    public string SelectedSystemName
    {
        get => Core.App.HostApp?.SelectedSystemName ?? "Not initialized";
    }

    public string SelectedSystemVariant
    {
        get => Core.App.HostApp?.SelectedSystemConfigurationVariant ?? "Not initialized";
    }

    public ObservableCollection<string> AvailableSystemVariants { get; } = new();

    public EmulatorState EmulatorState
    {
        get => Core.App.HostApp?.EmulatorState ?? EmulatorState.Uninitialized;
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

    // C64 Menu ViewModel
    public C64MenuViewModel C64Settings { get; private set; }

    public MainViewModel()
    {
        // Initialize C64 Menu ViewModel
        C64Settings = new C64MenuViewModel(this);

        // Initialize with available systems when HostApp is ready
        InitializeAvailableSystems();
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

            // Notify C64-specific property changes
            C64Settings.NotifyEmulatorStateChanged();
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
        if (Core.App.HostApp != null)
        {
            // Use ConfigureAwait(false) to avoid deadlock and run async operation in background
            _ = Task.Run(async () =>
            {
                try
                {
                    var (isValid, validationErrors) = await Core.App.HostApp.IsValidConfigWithDetails();

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

        if (Core.App.HostApp?.SystemList != null)
        {
            foreach (var systemName in Core.App.HostApp.SystemList.Systems)
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

        if (Core.App.HostApp?.AllSelectedSystemConfigurationVariants != null)
        {
            foreach (var variant in Core.App.HostApp.AllSelectedSystemConfigurationVariants)
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
        C64Settings.NotifyDiskImageStateChanged();
    }
}
