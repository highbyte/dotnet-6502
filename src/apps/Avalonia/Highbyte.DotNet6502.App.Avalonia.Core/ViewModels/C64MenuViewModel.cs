using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Utils;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class C64MenuViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _avaloniaHostApp;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly HttpClient? _appUrlHttpClient;

    private readonly Assembly _examplesAssembly = Assembly.GetExecutingAssembly();
    private string? ExampleFileAssemblyName => _examplesAssembly.GetName().Name;

    public AvaloniaHostApp HostApp => _avaloniaHostApp;

    public C64MenuViewModel(
        AvaloniaHostApp avaloniaHostApp,
        EmulatorConfig emulatorConfig)
    {
        _avaloniaHostApp = avaloniaHostApp ?? throw new ArgumentNullException(nameof(avaloniaHostApp));
        _emulatorConfig = emulatorConfig;
        _appUrlHttpClient = emulatorConfig.GetAppUrlHttpClient();

        _examplesAssembly = Assembly.GetExecutingAssembly();
        InitializeC64Data();
    }

    // Properties that previously accessed MainViewModel can now access HostApp directly
    public EmulatorState EmulatorState => _avaloniaHostApp.EmulatorState;
    public string SelectedSystemName => _avaloniaHostApp.SelectedSystemName;

    // C64-specific properties
    private C64HostConfig? C64HostConfig => _avaloniaHostApp?.CurrentHostSystemConfig as C64HostConfig;

    // Copy/Paste functionality
    public bool IsCopyPasteEnabled => EmulatorState == EmulatorState.Running;

    // AI Basic coding assistant
    public bool BasicCodingAssistantEnabled
    {
        get
        {
            if (EmulatorState != EmulatorState.Running)
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
    public bool BasicCodingAssistantAvailable => EmulatorState == EmulatorState.Running;

    // Configuration functionality
    public bool IsC64ConfigEnabled => EmulatorState == EmulatorState.Uninitialized;

    // Disk Drive functionality
    public bool IsDiskImageAttached
    {
        get
        {
            try
            {
                if (EmulatorState == EmulatorState.Uninitialized)
                    return false;

                var c64 = _avaloniaHostApp?.CurrentRunningSystem as Systems.Commodore64.C64;
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
    public int CurrentJoystick
    {
        get
        {
            return C64HostConfig?.InputConfig?.CurrentJoystick ?? 1;
        }
        set
        {
            if (C64HostConfig?.InputConfig != null)
            {
                var oldValue = C64HostConfig.InputConfig.CurrentJoystick;
                C64HostConfig.InputConfig.CurrentJoystick = value;

                if (oldValue != value)
                {
                    if (EmulatorState != EmulatorState.Uninitialized)
                    {
                        // TODO: Does a running C64 not have it's own setting for current joystick (like it has for if Joystick keyboard is enabled or not)?
                        //C64 c64 = (C64)_avaloniaHostApp!.CurrentRunningSystem!;
                    }
                    else
                    {
                        // If not running, update the config so it will be used when starting the system
                        _avaloniaHostApp?.UpdateHostSystemConfig(C64HostConfig);
                    }
                    this.RaisePropertyChanged();
                }
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
                    C64 c64 = (C64)_avaloniaHostApp!.CurrentRunningSystem!;
                    c64.Cia1.Joystick.KeyboardJoystickEnabled = value;
                }
                else
                {
                    // If not running, update the config so it will be used when starting the system
                    _avaloniaHostApp?.UpdateHostSystemConfig(C64HostConfig);
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
                    C64 c64 = (C64)_avaloniaHostApp!.CurrentRunningSystem!;
                    c64.Cia1.Joystick.KeyboardJoystick = value;
                }
                else
                {
                    // If not running, update the config so it will be used when starting the system
                    _avaloniaHostApp?.UpdateHostSystemConfig(C64HostConfig);
                }
                this.RaisePropertyChanged();
            }
        }
    }

    public bool IsKeyboardJoystickSelectionEnabled => JoystickKeyboardEnabled;

    // File operation enabled states
    public bool IsFileOperationEnabled => EmulatorState != EmulatorState.Uninitialized;

    // Configuration validation
    public bool HasConfigValidationErrors
    {
        get
        {
            if (C64HostConfig == null)
                return false;

            return !C64HostConfig.IsValid(out _);
        }
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

        // Debug: List all embedded resource names
        var resourceNames = _examplesAssembly.GetManifestResourceNames();
        foreach (var name in resourceNames)
        {
            System.Console.WriteLine(name);
        }
        // Initialize assembly examples
        AssemblyExamples.Clear();
        AssemblyExamples.Add(new KeyValuePair<string, string>("", "-- Select an example --"));
        AssemblyExamples.Add(new KeyValuePair<string, string>($"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.C64.smooth_scroller_and_raster.prg", "SmoothScroller"));
        AssemblyExamples.Add(new KeyValuePair<string, string>($"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.C64.scroller_and_raster.prg", "Scroller"));
        // Initialize basic examples
        BasicExamples.Clear();
        BasicExamples.Add(new KeyValuePair<string, string>("", "-- Select an example --"));
        BasicExamples.Add(new KeyValuePair<string, string>($"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Basic.C64.HelloWorld.prg", "HelloWorld"));
        BasicExamples.Add(new KeyValuePair<string, string>($"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Basic.C64.PlaySoundVoice1TriangleScale.prg", "PlaySound"));
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

    /// <summary>
    /// Notify C64-specific property changes when emulator state changes
    /// </summary>
    public void NotifyEmulatorStateChanged()
    {
        try
        {
            this.RaisePropertyChanged(nameof(EmulatorState));
            this.RaisePropertyChanged(nameof(SelectedSystemName));

            // Notify C64-specific property changes
            this.RaisePropertyChanged(nameof(IsCopyPasteEnabled));
            this.RaisePropertyChanged(nameof(BasicCodingAssistantAvailable));
            this.RaisePropertyChanged(nameof(IsC64ConfigEnabled));
            this.RaisePropertyChanged(nameof(IsDiskImageAttached));
            this.RaisePropertyChanged(nameof(DiskToggleButtonText));
            this.RaisePropertyChanged(nameof(IsFileOperationEnabled));
            this.RaisePropertyChanged(nameof(JoystickKeyboardEnabled));
            this.RaisePropertyChanged(nameof(KeyboardJoystick));
            this.RaisePropertyChanged(nameof(IsKeyboardJoystickSelectionEnabled));
            this.RaisePropertyChanged(nameof(CurrentJoystick));
            this.RaisePropertyChanged(nameof(HasConfigValidationErrors));
        }
        catch (Exception ex)
        {
            // Safe error handling for WebAssembly/AOT environments
            try
            {
                System.Console.WriteLine($"Error in C64MenuViewModel.NotifyEmulatorStateChanged: {ex?.Message ?? "Unknown error"}");
            }
            catch
            {
                System.Console.WriteLine("Error in C64MenuViewModel.NotifyEmulatorStateChanged: Unable to access exception details");
            }
        }
    }

    public async Task LoadAssemblyExample()
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized)
            return;

        string? file = SelectedAssemblyExample;
        if (string.IsNullOrEmpty(file))
            return;

        bool wasRunning = HostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            HostApp.Pause();

        try
        {
            byte[] prgBytes;
            // Load the .prg file from embedded resource
            using (var resourceStream = _examplesAssembly.GetManifestResourceStream(file))
            {
                if (resourceStream == null)
                    throw new Exception($"Cannot find file in embedded resources. Resource: {file}");
                // Read contents of stream as byte array
                prgBytes = new byte[resourceStream.Length];
                resourceStream.ReadExactly(prgBytes);
            }

            // Load file into memory
            BinaryLoader.Load(
                HostApp.CurrentRunningSystem!.Mem,
                prgBytes,
                out ushort loadedAtAddress,
                out ushort fileLength);

            // Set Program Counter to start of loaded file
            HostApp.CurrentRunningSystem.CPU.PC = loadedAtAddress;

            System.Console.WriteLine($"Assembly example loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
            System.Console.WriteLine($"Program Counter set to {loadedAtAddress.ToHex()}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading assembly example: {ex.Message}");
            throw;
        }
        finally
        {
            if (wasRunning)
                await HostApp.Start();
        }
    }

}
