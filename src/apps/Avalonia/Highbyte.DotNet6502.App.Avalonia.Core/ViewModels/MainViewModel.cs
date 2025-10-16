using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Linq;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
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

    // C64-specific properties
    private C64HostConfig? C64HostConfig => Core.App.HostApp?.CurrentHostSystemConfig as C64HostConfig;

    // Copy/Paste functionality
    public bool IsCopyPasteEnabled => EmulatorState == EmulatorState.Running && IsC64SystemSelected;

    // AI Basic coding assistant
    public bool BasicCodingAssistantEnabled
    {
        get
        {
            if (EmulatorState != EmulatorState.Running || !IsC64SystemSelected)
                return false;
            // Implementation will be added in code-behind
            return false; // Placeholder
        }
        set
        {
            // Implementation will be added in code-behind
            this.RaisePropertyChanged();
        }
    }
    public bool BasicCodingAssistantAvailable => EmulatorState == EmulatorState.Running && IsC64SystemSelected;

    // Disk Drive functionality
    public bool IsDiskImageAttached
    {
        get
        {
            try
            {
                if (EmulatorState == EmulatorState.Uninitialized || !IsC64SystemSelected)
                    return false;

                var c64 = Core.App.HostApp?.CurrentRunningSystem as Systems.Commodore64.C64;
                var diskDrive = c64?.IECBus?.Devices?.OfType<Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541>().FirstOrDefault();
                return diskDrive?.IsDisketteInserted == true;
            }
            catch
            {
                return false;
            }
        }
    }
    public string DiskToggleButtonText => IsDiskImageAttached ? "Detach .d64 disk image" : "Attach .d64 disk image";

    // Preloaded D64 programs
    public ObservableCollection<KeyValuePair<string, string>> PreloadedD64Programs { get; } = new();
    private string _selectedPreloadedDisk = "";
    public string SelectedPreloadedDisk
    {
        get => _selectedPreloadedDisk;
        set => this.RaiseAndSetIfChanged(ref _selectedPreloadedDisk, value);
    }
    public bool IsLoadingPreloadedDisk { get; private set; }

    // Assembly examples
    public ObservableCollection<KeyValuePair<string, string>> AssemblyExamples { get; } = new();
    private string _selectedAssemblyExample = "";
    public string SelectedAssemblyExample
    {
        get => _selectedAssemblyExample;
        set => this.RaiseAndSetIfChanged(ref _selectedAssemblyExample, value);
    }

    // Basic examples
    public ObservableCollection<KeyValuePair<string, string>> BasicExamples { get; } = new();
    private string _selectedBasicExample = "";
    public string SelectedBasicExample
    {
        get => _selectedBasicExample;
        set => this.RaiseAndSetIfChanged(ref _selectedBasicExample, value);
    }

    // Configuration
    public ObservableCollection<int> AvailableJoysticks { get; } = new();
    private int _currentJoystick = 1;
    public int CurrentJoystick
    {
        get => _currentJoystick;
        set
        {
            var oldValue = _currentJoystick;
            _currentJoystick = value;

            if (oldValue != value)
            {
                // Update the config when joystick changes
                if (C64HostConfig != null)
                {
                    C64HostConfig.InputConfig.CurrentJoystick = value;

                    if (EmulatorState != EmulatorState.Uninitialized)
                    {
                        // TODO: Does a running C64 not have it's own setting for current joystick (like it has for if Joystick keyboard is enabled or not)?
                        //C64 c64 = (C64)App.HostApp!.CurrentRunningSystem!;
                    }
                    else
                    {
                        // If not running, update the config so it will be used when starting the system
                        App.HostApp?.UpdateHostSystemConfig(C64HostConfig);
                    }
                }
                this.RaisePropertyChanged();
            }
        }
    }

    public bool JoystickKeyboardEnabled
    {
        get
        {
            return C64HostConfig?.SystemConfig?.KeyboardJoystickEnabled ?? false;
        }
        set
        {
            if (C64HostConfig?.SystemConfig != null)
            {
                C64HostConfig.SystemConfig.KeyboardJoystickEnabled = value;

                // If system is running, make sure to update the joystick setting in the running system
                if (EmulatorState != EmulatorState.Uninitialized)
                {
                    C64 c64 = (C64)App.HostApp!.CurrentRunningSystem!;
                    c64.Cia1.Joystick.KeyboardJoystickEnabled = value;
                }
                else
                {
                    // If not running, update the config so it will be used when starting the system
                    App.HostApp?.UpdateHostSystemConfig(C64HostConfig);
                }
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(IsKeyboardJoystickSelectionEnabled));
            }
        }
    }

    public int KeyboardJoystick
    {
        get
        {
            return C64HostConfig?.SystemConfig?.KeyboardJoystick ?? 1;
        }
        set
        {
            if (C64HostConfig?.SystemConfig != null)
            {
                C64HostConfig.SystemConfig.KeyboardJoystick = value;

                // If system is running, make sure to update the joystick setting in the running system
                if (EmulatorState != EmulatorState.Uninitialized)
                {
                    C64 c64 = (C64)App.HostApp!.CurrentRunningSystem!;
                    c64.Cia1.Joystick.KeyboardJoystick = value;
                }
                else
                {
                    // If not running, update the config so it will be used when starting the system
                    App.HostApp?.UpdateHostSystemConfig(C64HostConfig);
                }
                this.RaisePropertyChanged();
            }
        }
    }

    public bool IsKeyboardJoystickSelectionEnabled => JoystickKeyboardEnabled;

    // File operation enabled states
    public bool IsFileOperationEnabled => EmulatorState != EmulatorState.Uninitialized && IsC64SystemSelected;

    public MainViewModel()
    {
        // Initialize data collections  
        InitializeC64Data();

        // Initialize with available systems when HostApp is ready
        InitializeAvailableSystems();
    }

    private void InitializeC64Data()
    {
        // Initialize joystick options
        AvailableJoysticks.Clear();
        AvailableJoysticks.Add(1);
        AvailableJoysticks.Add(2);

        // Initialize preloaded D64 programs
        PreloadedD64Programs.Clear();
        PreloadedD64Programs.Add(new KeyValuePair<string, string>("", "-- Select a program --"));
        PreloadedD64Programs.Add(new KeyValuePair<string, string>("bubblebobble", "Bubble Bobble"));
        PreloadedD64Programs.Add(new KeyValuePair<string, string>("digiloi", "Digiloi"));
        PreloadedD64Programs.Add(new KeyValuePair<string, string>("elite", "Elite"));
        PreloadedD64Programs.Add(new KeyValuePair<string, string>("lastninja", "Last Ninja"));
        PreloadedD64Programs.Add(new KeyValuePair<string, string>("minizork", "Mini Zork"));
        PreloadedD64Programs.Add(new KeyValuePair<string, string>("montezuma", "Montezuma's Revenge"));
        PreloadedD64Programs.Add(new KeyValuePair<string, string>("rallyspeedway", "Rally Speedway"));

        // Initialize assembly examples
        AssemblyExamples.Clear();
        AssemblyExamples.Add(new KeyValuePair<string, string>("", "-- Select an example --"));
        AssemblyExamples.Add(new KeyValuePair<string, string>("6502binaries/C64/Assembler/smooth_scroller_and_raster.prg", "SmoothScroller"));
        AssemblyExamples.Add(new KeyValuePair<string, string>("6502binaries/C64/Assembler/scroller_and_raster.prg", "Scroller"));

        // Initialize basic examples
        BasicExamples.Clear();
        BasicExamples.Add(new KeyValuePair<string, string>("", "-- Select an example --"));
        BasicExamples.Add(new KeyValuePair<string, string>("6502binaries/C64/Basic/HelloWorld.prg", "HelloWorld"));
        BasicExamples.Add(new KeyValuePair<string, string>("6502binaries/C64/Basic/PlaySoundVoice1TriangleScale.prg", "PlaySound"));
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
            this.RaisePropertyChanged(nameof(IsCopyPasteEnabled));
            this.RaisePropertyChanged(nameof(BasicCodingAssistantAvailable));
            this.RaisePropertyChanged(nameof(IsDiskImageAttached));
            this.RaisePropertyChanged(nameof(DiskToggleButtonText));
            this.RaisePropertyChanged(nameof(IsFileOperationEnabled));
            this.RaisePropertyChanged(nameof(JoystickKeyboardEnabled));
            this.RaisePropertyChanged(nameof(KeyboardJoystick));
            this.RaisePropertyChanged(nameof(IsKeyboardJoystickSelectionEnabled));
            this.RaisePropertyChanged(nameof(CurrentJoystick));
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
        try
        {
            this.RaisePropertyChanged(nameof(IsDiskImageAttached));
            this.RaisePropertyChanged(nameof(DiskToggleButtonText));
        }
        catch (Exception ex)
        {
            // Safe error handling for WebAssembly/AOT environments
            try
            {
                System.Console.WriteLine($"Error in NotifyDiskImageStateChanged: {ex?.Message ?? "Unknown error"}");
            }
            catch
            {
                System.Console.WriteLine("Error in NotifyDiskImageStateChanged: Unable to access exception details");
            }
        }
    }
}
