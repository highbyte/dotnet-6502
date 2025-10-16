using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
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

    // Commands for interacting with the emulator
    // Use ICommand interface for WebAssembly compatibility
    public ICommand StartEmulatorCommand { get; private set; }
    public ICommand StopEmulatorCommand { get; private set; }
    public ICommand PauseEmulatorCommand { get; private set; }
    public ICommand ResetEmulatorCommand { get; private set; }
    public ICommand SelectSystemCommand { get; private set; }
    public ICommand SelectSystemVariantCommand { get; private set; }

    // C64-specific properties and commands
    private C64HostConfig? C64HostConfig => Core.App.HostApp?.CurrentHostSystemConfig as C64HostConfig;
    
    // Copy/Paste functionality
    public ICommand CopyBasicSourceCommand { get; private set; }
    public ICommand PasteTextCommand { get; private set; }
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
    public ICommand ToggleDiskImageCommand { get; private set; }
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
    public ICommand LoadPreloadedDiskCommand { get; private set; }
    public bool IsLoadingPreloadedDisk { get; private set; }

    // Load/Save functionality
    public ICommand LoadBasicFileCommand { get; private set; }
    public ICommand SaveBasicFileCommand { get; private set; }
    public ICommand LoadBinaryFileCommand { get; private set; }
    
    // Assembly examples
    public ObservableCollection<KeyValuePair<string, string>> AssemblyExamples { get; } = new();
    private string _selectedAssemblyExample = "";
    public string SelectedAssemblyExample
    {
        get => _selectedAssemblyExample;
        set => this.RaiseAndSetIfChanged(ref _selectedAssemblyExample, value);
    }
    public ICommand LoadAssemblyExampleCommand { get; private set; }
    
    // Basic examples
    public ObservableCollection<KeyValuePair<string, string>> BasicExamples { get; } = new();
    private string _selectedBasicExample = "";
    public string SelectedBasicExample
    {
        get => _selectedBasicExample;
        set => this.RaiseAndSetIfChanged(ref _selectedBasicExample, value);
    }
    public ICommand LoadBasicExampleCommand { get; private set; }

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
                    Core.App.HostApp?.UpdateHostSystemConfig(C64HostConfig);
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
                Core.App.HostApp?.UpdateHostSystemConfig(C64HostConfig);
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
                Core.App.HostApp?.UpdateHostSystemConfig(C64HostConfig);
                this.RaisePropertyChanged();
            }
        }
    }

    public bool IsKeyboardJoystickSelectionEnabled => JoystickKeyboardEnabled;

    // File operation enabled states
    public bool IsFileOperationEnabled => EmulatorState != EmulatorState.Uninitialized && IsC64SystemSelected;

    public MainViewModel()
    {
        // Initialize commands with WebAssembly compatibility
        InitializeCommands();

        // Initialize with available systems when HostApp is ready
        InitializeAvailableSystems();
    }

    private void InitializeCommands()
    {
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            // WebAssembly: Use simple command implementations to avoid ReactiveCommand issues
            StartEmulatorCommand = new WebAssemblyCommand(() => StartEmulatorSync());
            StopEmulatorCommand = new WebAssemblyCommand(() => StopEmulator());
            PauseEmulatorCommand = new WebAssemblyCommand(() => PauseEmulator());
            ResetEmulatorCommand = new WebAssemblyCommand(() => ResetEmulator());
            SelectSystemCommand = new WebAssemblyCommand<string>(systemName => SelectSystemSync(systemName));
            SelectSystemVariantCommand = new WebAssemblyCommand<string>(variant => SelectSystemVariantSync(variant));
            
            // C64-specific commands - these will be handled by events for now
            CopyBasicSourceCommand = new WebAssemblyCommand(() => { });
            PasteTextCommand = new WebAssemblyCommand(() => { });
            ToggleDiskImageCommand = new WebAssemblyCommand(() => { });
            LoadPreloadedDiskCommand = new WebAssemblyCommand(() => { });
            LoadBasicFileCommand = new WebAssemblyCommand(() => { });
            SaveBasicFileCommand = new WebAssemblyCommand(() => { });
            LoadBinaryFileCommand = new WebAssemblyCommand(() => { });
            LoadAssemblyExampleCommand = new WebAssemblyCommand(() => { });
            LoadBasicExampleCommand = new WebAssemblyCommand(() => { });
        }
        else
        {
            try
            {
                // Desktop: Use ReactiveCommand for full functionality
                StartEmulatorCommand = ReactiveCommand.CreateFromTask(StartEmulator);
                StopEmulatorCommand = ReactiveCommand.Create(StopEmulator);
                PauseEmulatorCommand = ReactiveCommand.Create(PauseEmulator);
                ResetEmulatorCommand = ReactiveCommand.Create(ResetEmulator);
                SelectSystemCommand = ReactiveCommand.CreateFromTask<string>(SelectSystemAsync);
                SelectSystemVariantCommand = ReactiveCommand.CreateFromTask<string>(SelectSystemVariantAsync);
                
                // C64-specific commands - these will be handled by events for now
                CopyBasicSourceCommand = ReactiveCommand.Create(() => { });
                PasteTextCommand = ReactiveCommand.Create(() => { });
                ToggleDiskImageCommand = ReactiveCommand.Create(() => { });
                LoadPreloadedDiskCommand = ReactiveCommand.Create(() => { });
                LoadBasicFileCommand = ReactiveCommand.Create(() => { });
                SaveBasicFileCommand = ReactiveCommand.Create(() => { });
                LoadBinaryFileCommand = ReactiveCommand.Create(() => { });
                LoadAssemblyExampleCommand = ReactiveCommand.Create(() => { });
                LoadBasicExampleCommand = ReactiveCommand.Create(() => { });
            }
            catch (Exception ex)
            {
                // Fallback to simple commands if ReactiveCommand fails
                System.Console.WriteLine($"Warning: ReactiveCommand initialization failed, using fallback commands: {ex.Message}");
                StartEmulatorCommand = new WebAssemblyCommand(() => StartEmulatorSync());
                StopEmulatorCommand = new WebAssemblyCommand(() => StopEmulator());
                PauseEmulatorCommand = new WebAssemblyCommand(() => PauseEmulator());
                ResetEmulatorCommand = new WebAssemblyCommand(() => ResetEmulator());
                SelectSystemCommand = new WebAssemblyCommand<string>(systemName => SelectSystemSync(systemName));
                SelectSystemVariantCommand = new WebAssemblyCommand<string>(variant => SelectSystemVariantSync(variant));
                
                // C64-specific commands fallback
                CopyBasicSourceCommand = new WebAssemblyCommand(() => { });
                PasteTextCommand = new WebAssemblyCommand(() => { });
                ToggleDiskImageCommand = new WebAssemblyCommand(() => { });
                LoadPreloadedDiskCommand = new WebAssemblyCommand(() => { });
                LoadBasicFileCommand = new WebAssemblyCommand(() => { });
                SaveBasicFileCommand = new WebAssemblyCommand(() => { });
                LoadBinaryFileCommand = new WebAssemblyCommand(() => { });
                LoadAssemblyExampleCommand = new WebAssemblyCommand(() => { });
                LoadBasicExampleCommand = new WebAssemblyCommand(() => { });
            }
        }
        
        // Initialize data collections
        InitializeC64Data();
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

    /// <summary>
    /// Synchronous wrapper for StartEmulator for WebAssembly compatibility
    /// </summary>
    private void StartEmulatorSync()
    {
        // Fire and forget the async operation to avoid blocking the UI thread
        _ = Task.Run(async () =>
        {
            try
            {
                await StartEmulator();
            }
            catch (Exception ex)
            {
                // Safe error handling for WebAssembly/AOT environments
                var errorMessage = "Unknown error";
                try
                {
                    errorMessage = ex?.Message ?? "Exception message was null";
                }
                catch
                {
                    errorMessage = "Error accessing exception message";
                }
                
                System.Console.WriteLine($"Error starting emulator: {errorMessage}");
                
                // Also log exception type if possible
                try
                {
                    System.Console.WriteLine($"Exception type: {ex?.GetType()?.Name ?? "Unknown"}");
                }
                catch
                {
                    // Ignore errors when accessing exception type
                }
                
                // Ensure UI is updated even if there's an error
                global::Avalonia.Threading.Dispatcher.UIThread.Post(NotifyEmulatorStateChanged);
            }
        });
    }

    /// <summary>
    /// Synchronous wrapper for SelectSystemAsync for WebAssembly compatibility
    /// </summary>
    private void SelectSystemSync(string systemName)
    {
        // Fire and forget the async operation to avoid blocking the UI thread
        _ = Task.Run(async () =>
        {
            try
            {
                await SelectSystemAsync(systemName);
            }
            catch (Exception ex)
            {
                // Safe error handling for WebAssembly/AOT environments
                var errorMessage = "Unknown error";
                try
                {
                    errorMessage = ex?.Message ?? "Exception message was null";
                }
                catch
                {
                    errorMessage = "Error accessing exception message";
                }
                
                System.Console.WriteLine($"Error selecting system: {errorMessage}");
                
                // Also log exception type if possible
                try
                {
                    System.Console.WriteLine($"Exception type: {ex?.GetType()?.Name ?? "Unknown"}");
                }
                catch
                {
                    // Ignore errors when accessing exception type
                }
                
                // Ensure UI is updated even if there's an error
                global::Avalonia.Threading.Dispatcher.UIThread.Post(NotifyEmulatorStateChanged);
            }
        });
    }

    /// <summary>
    /// Synchronous wrapper for SelectSystemVariantAsync for WebAssembly compatibility
    /// </summary>
    private void SelectSystemVariantSync(string variant)
    {
        // Fire and forget the async operation to avoid blocking the UI thread
        _ = Task.Run(async () =>
        {
            try
            {
                await SelectSystemVariantAsync(variant);
            }
            catch (Exception ex)
            {
                // Safe error handling for WebAssembly/AOT environments
                var errorMessage = "Unknown error";
                try
                {
                    errorMessage = ex?.Message ?? "Exception message was null";
                }
                catch
                {
                    errorMessage = "Error accessing exception message";
                }
                
                System.Console.WriteLine($"Error selecting system variant: {errorMessage}");
                
                // Also log exception type if possible
                try
                {
                    System.Console.WriteLine($"Exception type: {ex?.GetType()?.Name ?? "Unknown"}");
                }
                catch
                {
                    // Ignore errors when accessing exception type
                }
                
                // Ensure UI is updated even if there's an error
                global::Avalonia.Threading.Dispatcher.UIThread.Post(NotifyEmulatorStateChanged);
            }
        });
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

    private async Task SelectSystemAsync(string systemName)
    {
        try
        {
            if (Core.App.HostApp != null && Core.App.HostApp.SelectedSystemName != systemName)
            {
                await Core.App.HostApp.SelectSystem(systemName);
                // Update available variants when system changes
                UpdateAvailableVariants();
                // Notify UI that the selection has changed
                NotifyEmulatorStateChanged();
            }
        }
        catch (Exception)
        {
            // Handle exception if needed
            // The UI will still reflect the actual state from HostApp
            NotifyEmulatorStateChanged();
        }
    }

    private async Task SelectSystemVariantAsync(string variant)
    {
        try
        {
            if (Core.App.HostApp != null && Core.App.HostApp.SelectedSystemConfigurationVariant != variant)
            {
                await Core.App.HostApp.SelectSystemConfigurationVariant(variant);
                // Notify UI that the selection has changed
                NotifyEmulatorStateChanged();
            }
        }
        catch (Exception)
        {
            // Handle exception if needed
            // The UI will still reflect the actual state from HostApp
            NotifyEmulatorStateChanged();
        }
    }

    private async Task StartEmulator()
    {
        try
        {
            // Additional null checks for WebAssembly safety
            if (Core.App.HostApp == null)
            {
                System.Console.WriteLine("Error starting emulator: HostApp is null");
                return;
            }

            if (string.IsNullOrEmpty(SelectedSystemName))
            {
                System.Console.WriteLine("Error starting emulator: SelectedSystemName is null or empty");
                return;
            }

            if (SelectedSystemName == "Loading...")
            {
                System.Console.WriteLine("Error starting emulator: System is still loading");
                return;
            }

            // WebAssembly-specific: Ensure the system is properly selected before starting
            // This addresses potential race conditions where the UI is ready before system initialization
            if (PlatformDetection.IsRunningInWebAssembly())
            {
                // Double-check that the selected system name matches what the HostApp has
                if (Core.App.HostApp.SelectedSystemName != SelectedSystemName)
                {
                    System.Console.WriteLine($"Warning: Re-selecting system {SelectedSystemName} to ensure proper initialization");
                    try
                    {
                        await Core.App.HostApp.SelectSystem(SelectedSystemName);
                    }
                    catch (Exception selectEx)
                    {
                        System.Console.WriteLine($"Error re-selecting system: {selectEx.Message}");
                        return;
                    }
                }

                // Additional safety check: Ensure the system is properly configured
                try
                {
                    var isValid = await Core.App.HostApp.IsSystemConfigValid();
                    if (!isValid)
                    {
                        System.Console.WriteLine("Error starting emulator: System configuration is not valid");
                        return;
                    }
                }
                catch (Exception configEx)
                {
                    System.Console.WriteLine($"Error checking system config: {configEx.Message}");
                    return;
                }
            }

            // System selection is now handled in the SelectedSystemName setter
            // Just start the emulation
            Console.WriteLine($"Starting emulator with system: {SelectedSystemName}");
            await Core.App.HostApp.Start();
            Console.WriteLine("Emulator started successfully");
        }
        catch (Exception ex)
        {
            // Safe error handling for WebAssembly/AOT environments
            var errorMessage = "Unknown error";
            try
            {
                errorMessage = ex?.Message ?? "Exception message was null";
            }
            catch
            {
                errorMessage = "Error accessing exception message";
            }

            System.Console.WriteLine($"Error in StartEmulator: {errorMessage}");

            // Handle exception if needed
            throw;
        }
        finally
        {
            // Always notify that state may have changed
            NotifyEmulatorStateChanged();
        }
    }

    private void StopEmulator()
    {
        try
        {
            Core.App.HostApp?.Stop();
        }
        catch (Exception)
        {
            // Handle exception if needed
        }
        finally
        {
            // Always notify that state may have changed
            NotifyEmulatorStateChanged();
        }
    }

    private void PauseEmulator()
    {
        try
        {
            Core.App.HostApp?.Pause();
        }
        catch (Exception)
        {
            // Handle exception if needed
        }
        finally
        {
            // Always notify that state may have changed
            NotifyEmulatorStateChanged();
        }
    }

    private void ResetEmulator()
    {
        try
        {
            Core.App.HostApp?.Reset();
        }
        catch (Exception)
        {
            // Handle exception if needed
        }
        finally
        {
            // Always notify that state may have changed
            NotifyEmulatorStateChanged();
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

/// <summary>
/// Simple ICommand implementation for WebAssembly compatibility
/// Avoids ReactiveCommand issues in AOT/WebAssembly environments
/// </summary>
public class WebAssemblyCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public WebAssemblyCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// Generic simple ICommand implementation for WebAssembly compatibility
/// </summary>
public class WebAssemblyCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    public WebAssemblyCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter)
    {
        if (parameter is T typedParameter)
            return _canExecute?.Invoke(typedParameter) ?? true;
        return false;
    }

    public void Execute(object? parameter)
    {
        if (parameter is T typedParameter)
            _execute(typedParameter);
    }
}
