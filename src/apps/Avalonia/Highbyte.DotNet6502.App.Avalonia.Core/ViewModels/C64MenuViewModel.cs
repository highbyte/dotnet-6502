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
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class C64MenuViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _avaloniaHostApp;

    private readonly Assembly _examplesAssembly = Assembly.GetExecutingAssembly();
    private string? ExampleFileAssemblyName => _examplesAssembly.GetName().Name;

    // Fields for preloaded disk loading functionality
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<string, D64DownloadDiskInfo> _preloadedD64Images = new()
    {
        {"bubblebobble", new D64DownloadDiskInfo("Bubble Bobble", "https://csdb.dk/release/download.php?id=191127", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: false, directLoadPRGName: "*")}, // Note: Bubble Bobble is not a bitmap game, but somehow this version fails to initialize the custom charset in text mode correctly in SkiaSharp renderer.
        {"digiloi", new D64DownloadDiskInfo("Digiloi", "https://csdb.dk/release/download.php?id=213381", keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, audioEnabled: true, directLoadPRGName: "*")},
        {"elite", new D64DownloadDiskInfo("Elite", "https://csdb.dk/release/download.php?id=70413", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: false,directLoadPRGName: "*", c64Variant: "C64PAL")},
        {"lastninja", new D64DownloadDiskInfo("Last Ninja", "https://csdb.dk/release/download.php?id=101848", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: false, directLoadPRGName: "*")},
        {"minizork", new D64DownloadDiskInfo("Mini Zork", "https://csdb.dk/release/download.php?id=42919", audioEnabled: false, directLoadPRGName: "*")},
        {"montezuma", new D64DownloadDiskInfo("Montezuma's Revenge", "https://csdb.dk/release/download.php?id=128101", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, audioEnabled: true, directLoadPRGName: "*")},
        {"rallyspeedway", new D64DownloadDiskInfo("Rally Speedway", "https://csdb.dk/release/download.php?id=219614", keyboardJoystickEnabled: true, keyboardJoystickNumber: 1, audioEnabled: true, directLoadPRGName: "*")}
    };
    private string _latestPreloadedDiskError = "";
    private bool _isLoadingPreloadedDisk = false;
    private D64AutoDownloadAndRun? _d64AutoDownloadAndRun;


    public AvaloniaHostApp HostApp => _avaloniaHostApp;

    public C64MenuViewModel(
        AvaloniaHostApp avaloniaHostApp)
    {
        _avaloniaHostApp = avaloniaHostApp ?? throw new ArgumentNullException(nameof(avaloniaHostApp));

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
        // var resourceNames = _examplesAssembly.GetManifestResourceNames();
        // foreach (var name in resourceNames)
        // {
        //     System.Console.WriteLine(name);
        // }
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

    public async Task LoadPreloadedDiskImage()
    {
        string selectedPreloadedDisk = SelectedPreloadedDisk;
        if (string.IsNullOrEmpty(selectedPreloadedDisk) || !_preloadedD64Images.ContainsKey(selectedPreloadedDisk))
            return;

        var diskInfo = _preloadedD64Images[selectedPreloadedDisk];
        _isLoadingPreloadedDisk = true;
        _latestPreloadedDiskError = "";

        System.Console.WriteLine($"Starting to load preloaded disk: {diskInfo.DisplayName}");

        try
        {
            // Initialize D64AutoDownloadAndRun if not already done
            if (_d64AutoDownloadAndRun == null)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                var c64HostConfig = HostApp!.CurrentHostSystemConfig as C64HostConfig;
                _d64AutoDownloadAndRun = new D64AutoDownloadAndRun(
                   loggerFactory,
                   _httpClient,
                   HostApp!,
                    corsProxyUrl: PlatformDetection.IsRunningInWebAssembly() ? c64HostConfig.CorsProxyURL : null);
            }

            await _d64AutoDownloadAndRun.DownloadAndRunDiskImage(
                diskInfo,
                stateHasChangedCallback: async () =>
                {
                    NotifyEmulatorStateChanged();
                    await Task.CompletedTask;
                },
                setConfigCallback: async (diskInfo) =>
                {
                    if (HostApp?.CurrentHostSystemConfig is not C64HostConfig c64HostConfig)
                        return;

                    var c64SystemConfig = c64HostConfig.SystemConfig;

                    // Apply keyboard joystick settings to config object while emulator is stopped
                    c64SystemConfig.KeyboardJoystickEnabled = diskInfo.KeyboardJoystickEnabled;
                    c64SystemConfig.KeyboardJoystick = diskInfo.KeyboardJoystickNumber;

                    // Apply renderer setting to config object while emulator is stopped
                    // TODO: If/when a optimized RenderType for use without bitmap graphics is available, set rendererProviderType appropriately here.
                    //Type rendererProviderType = diskInfo.RequiresBitmap ? typeof(Vic2Rasterizer) : typeof(C64VideoCommandStream);
                    Type rendererProviderType = typeof(Vic2Rasterizer);
                    c64HostConfig.SystemConfig.SetRenderProviderType(rendererProviderType);

                    // Apply audio enabled setting to config object while emulator is stopped
                    c64SystemConfig.AudioEnabled = diskInfo.AudioEnabled;

                    // Apply C64 variant setting to config object while emulator is stopped
                    await HostApp.SelectSystemConfigurationVariant(diskInfo.C64Variant);

                    HostApp.UpdateHostSystemConfig(c64HostConfig);
                });
        }
        catch (Exception ex)
        {
            _latestPreloadedDiskError = $"Error downloading or running disk image: {ex.Message}";
            System.Console.WriteLine($"LoadPreloadedDisk_Click error: {_latestPreloadedDiskError}");
        }
        finally
        {
            _isLoadingPreloadedDisk = false;
            System.Console.WriteLine($"Finished loading preloaded disk. Loading state: {_isLoadingPreloadedDisk}");
            if (!string.IsNullOrEmpty(_latestPreloadedDiskError))
                System.Console.WriteLine($"Final error state: {_latestPreloadedDiskError}");
            NotifyEmulatorStateChanged();
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

    public async Task LoadBasicExample()
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized)
            return;

        string? file = SelectedBasicExample;
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

            var c64 = (C64)HostApp.CurrentRunningSystem!;
            if (loadedAtAddress != C64.BASIC_LOAD_ADDRESS)
            {
                // Probably not a Basic program that was loaded. Don't init BASIC memory variables.
                System.Console.WriteLine($"Warning: Loaded program is not a Basic program, it's expected to load at {C64.BASIC_LOAD_ADDRESS.ToHex()} but was loaded at {loadedAtAddress.ToHex()}");
            }
            else
            {
                // Init C64 BASIC memory variables
                c64.InitBasicMemoryVariables(loadedAtAddress, fileLength);
            }

            // Send "list" + NewLine (Return) to the keyboard buffer to immediately list the loaded program
            c64.TextPaste.Paste("list\n");

            System.Console.WriteLine($"Basic example loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading basic example: {ex.Message}");
        }
        finally
        {
            if (wasRunning)
                await HostApp.Start();
        }
    }

    public async Task LoadBasicFile(byte[] fileBuffer)
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized)
            return;

        bool wasRunning = HostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            HostApp.Pause();

        try
        {
            BinaryLoader.Load(
                HostApp.CurrentRunningSystem!.Mem,
                fileBuffer,
                out ushort loadedAtAddress,
                out ushort fileLength);

            if (loadedAtAddress != C64.BASIC_LOAD_ADDRESS)
            {
                System.Console.WriteLine($"Warning: Loaded program is not a Basic program, it's expected to load at {C64.BASIC_LOAD_ADDRESS.ToHex()} but was loaded at {loadedAtAddress.ToHex()}");
            }
            else
            {
                var c64 = (C64)HostApp.CurrentRunningSystem!;
                c64.InitBasicMemoryVariables(loadedAtAddress, fileLength);
            }
        }
        finally
        {
            if (wasRunning)
                await HostApp.Start();
        }
    }

    public async Task<byte[]> GetBasicProgramAsPrgFileBytes()
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized)
            return Array.Empty<byte>();

        bool wasRunning = HostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            HostApp.Pause();

        try
        {
            ushort startAddress = C64.BASIC_LOAD_ADDRESS;
            var c64 = (C64)HostApp.CurrentRunningSystem!;
            var endAddress = c64.GetBasicProgramEndAddress();

            var saveData = BinarySaver.BuildSaveData(
                HostApp.CurrentRunningSystem.Mem,
                startAddress,
                endAddress,
                addFileHeaderWithLoadAddress: true);

            return saveData;
        }
        finally
        {
            if (wasRunning)
                await HostApp.Start();
        }
    }

    public async Task LoadBinaryFile(byte[] fileBuffer)
    {
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized)
            return;

        bool wasRunning = HostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            HostApp.Pause();

        try
        {
            BinaryLoader.Load(
                HostApp.CurrentRunningSystem!.Mem,
                fileBuffer,
                out ushort loadedAtAddress,
                out ushort fileLength);

            HostApp.CurrentRunningSystem.CPU.PC = loadedAtAddress;

            System.Console.WriteLine($"Binary program loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
            System.Console.WriteLine($"Program Counter set to {loadedAtAddress.ToHex()}");

            await HostApp.Start();
        }
        finally
        {
            if (wasRunning && HostApp.EmulatorState != Systems.EmulatorState.Running)
                await HostApp.Start();
        }
    }
}
