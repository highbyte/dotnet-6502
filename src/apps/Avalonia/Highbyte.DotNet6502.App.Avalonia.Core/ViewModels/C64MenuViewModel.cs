using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Input;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.Impl.Avalonia.Commodore64.Input;
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
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public AvaloniaHostApp HostApp => _avaloniaHostApp;

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

    // --- ReactiveUI Commands ---
    public ReactiveCommand<Unit, Unit> CopyBasicSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteTextCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleDiskImageCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadPreloadedDiskCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadAssemblyExampleCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadBasicExampleCommand { get; }
    public ReactiveCommand<byte[], Unit> LoadBasicFileCommand { get; }
    public ReactiveCommand<Unit, byte[]> SaveBasicFileCommand { get; }
    public ReactiveCommand<byte[], Unit> LoadBinaryFileCommand { get; }
    // --- End ReactiveUI Commands ---

    public C64MenuViewModel(
        AvaloniaHostApp avaloniaHostApp,
        ILoggerFactory loggerFactory)
    {
        _avaloniaHostApp = avaloniaHostApp ?? throw new ArgumentNullException(nameof(avaloniaHostApp));
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(C64MenuViewModel).Name);

        _examplesAssembly = Assembly.GetExecutingAssembly();
        InitializeC64Data();

        _avaloniaHostApp
            .WhenAnyValue(x => x.EmulatorState)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsC64ConfigEnabled));
                this.RaisePropertyChanged(nameof(IsCopyPasteEnabled));
                this.RaisePropertyChanged(nameof(IsDiskImageAttached));
                this.RaisePropertyChanged(nameof(CanToggleDisk));
                this.RaisePropertyChanged(nameof(DiskToggleButtonText));
                this.RaisePropertyChanged(nameof(BasicCodingAssistantAvailable));
                this.RaisePropertyChanged(nameof(BasicCodingAssistantEnabled));
                this.RaisePropertyChanged(nameof(IsFileOperationEnabled));
            });

        // Subscribe to KeyDown events from AvaloniaHostApp
        _avaloniaHostApp.KeyDownEvent += OnHostKeyDown;
        _avaloniaHostApp.KeyUpEvent += OnHostKeyUp;

        // Initialize ReactiveCommands
        CopyBasicSourceCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await CopyBasicSourceCode(),
            _logger,
            this.WhenAnyValue(x => x.IsCopyPasteEnabled),
            RxApp.MainThreadScheduler);

        PasteTextCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await PasteTextInternal(),
            _logger,
            this.WhenAnyValue(x => x.IsCopyPasteEnabled),
            RxApp.MainThreadScheduler);

        ToggleDiskImageCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await ToggleDiskImageInternal(),
            _logger,
            this.WhenAnyValue(x => x.CanToggleDisk),
            RxApp.MainThreadScheduler);

        LoadPreloadedDiskCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await LoadPreloadedDiskImage(),
            _logger,
            Observable.Return(true),
            RxApp.MainThreadScheduler);

        LoadAssemblyExampleCommand = ReactiveCommandHelper.CreateSafeCommand(
             async () => await LoadAssemblyExample(),
            _logger,
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxApp.MainThreadScheduler);

        LoadBasicExampleCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await LoadBasicExample(),
            _logger,
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxApp.MainThreadScheduler);

        LoadBasicFileCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            async (fileBuffer) => await LoadBasicFile(fileBuffer),
            _logger,
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxApp.MainThreadScheduler);

        SaveBasicFileCommand = ReactiveCommandHelper.CreateSafeCommandWithResult<byte[]>(
            async () => await GetBasicProgramAsPrgFileBytes(),
            _logger,
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxApp.MainThreadScheduler);

        LoadBinaryFileCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            async (fileBuffer) => await LoadBinaryFile(fileBuffer),
            _logger,
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxApp.MainThreadScheduler);
    }

    private EmulatorState EmulatorState => _avaloniaHostApp.EmulatorState;

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
            return C64HostConfig != null
                ? ((AvaloniaC64InputHandler)HostApp.CurrentSystemRunner!.InputHandler).CodingAssistantEnabled : false;

        }
        set
        {
            ((AvaloniaC64InputHandler)HostApp.CurrentSystemRunner!.InputHandler).CodingAssistantEnabled = value;
            if (C64HostConfig != null)
            {
                C64HostConfig.BasicAIAssistantDefaultEnabled = value;
            }
            this.RaisePropertyChanged(nameof(BasicCodingAssistantEnabled));
        }
    }

    public bool BasicCodingAssistantAvailable => EmulatorState == EmulatorState.Running &&
            HostApp.CurrentRunningSystem is C64 &&
            ((AvaloniaC64InputHandler)HostApp.CurrentSystemRunner!.InputHandler).CodingAssistantAvailable;

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

    public bool CanToggleDisk => EmulatorState != EmulatorState.Uninitialized;

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
                this.RaisePropertyChanged(nameof(JoystickKeyboardEnabled));
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
    /// Force refresh of all data bindings in the ViewModel.
    /// Useful when multiple properties have changed and you want to notify the UI to update all bindings, like after changing system configuration.
    /// </summary>
    public void RefreshAllBindings()
    {
        this.RaisePropertyChanged(string.Empty); // Notifies all property bindings
    }

    // Core C64 functionality methods
    private async Task CopyBasicSourceCode()
    {
        if (_avaloniaHostApp?.EmulatorState != EmulatorState.Running ||
          !IsC64System())
            return;

        try
        {
            var c64 = (C64)_avaloniaHostApp.CurrentRunningSystem!;
            var sourceCode = c64.BasicTokenParser.GetBasicText();

            // Request View to copy to clipboard
            await RequestClipboardCopy(sourceCode.ToLower());
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error copying Basic source: {ex.Message}");
        }
    }

    private async Task PasteTextInternal()
    {
        if (_avaloniaHostApp?.EmulatorState != EmulatorState.Running || !IsC64System())
            return;

        try
        {
            // Request text from View's clipboard
            var text = await RequestClipboardPaste();
            if (!string.IsNullOrEmpty(text))
            {
                var c64 = (C64)_avaloniaHostApp.CurrentRunningSystem!;
                c64.TextPaste.Paste(text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error pasting text: {ex.Message}");
        }
    }

    private async Task ToggleDiskImageInternal()
    {
        if (_avaloniaHostApp?.EmulatorState == EmulatorState.Uninitialized || !IsC64System())
            return;

        try
        {
            var c64 = (C64)_avaloniaHostApp.CurrentRunningSystem!;
            var diskDrive = c64.IECBus?.Devices?.OfType<Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541>().FirstOrDefault();

            if (diskDrive?.IsDisketteInserted == true)
            {
                // Detach current disk image
                diskDrive.RemoveD64DiskImage();
            }
            else
            {
                // Request View to show file picker and attach disk image
                await RequestAttachDiskImage();
            }

            // Notify that the disk image state has changed
            RefreshAllBindings();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error toggling disk image: {ex.Message}");
        }
    }

    private bool IsC64System()
    {
        return string.Equals(_avaloniaHostApp?.SelectedSystemName, C64.SystemName, StringComparison.OrdinalIgnoreCase);
    }

    // Events for View to handle clipboard and file operations
    public event EventHandler<string>? ClipboardCopyRequested;
    public event EventHandler? ClipboardPasteRequested;
    public event EventHandler? AttachDiskImageRequested;

    // Properties for View to provide results
    public string? ClipboardPasteResult { get; set; }
    public byte[]? DiskImageFileResult { get; set; }

    private async Task RequestClipboardCopy(string text)
    {
        ClipboardCopyRequested?.Invoke(this, text);
        await Task.CompletedTask;
    }

    private async Task<string?> RequestClipboardPaste()
    {
        ClipboardPasteResult = null;
        ClipboardPasteRequested?.Invoke(this, EventArgs.Empty);
        // View should synchronously set ClipboardPasteResult
        await Task.CompletedTask;
        return ClipboardPasteResult;
    }

    private async Task RequestAttachDiskImage()
    {
        DiskImageFileResult = null;
        AttachDiskImageRequested?.Invoke(this, EventArgs.Empty);
        // View should handle file picker and set DiskImageFileResult
        await Task.Delay(100); // Give UI time to process

        if (DiskImageFileResult != null)
        {
            try
            {
                // Parse the D64 disk image
                var d64DiskImage = Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.D64Parser.ParseD64File(DiskImageFileResult);

                // Set the disk image on the running C64's DiskDrive1541
                var c64 = (C64)_avaloniaHostApp!.CurrentRunningSystem!;
                var diskDrive = c64.IECBus?.Devices?.OfType<Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541>().FirstOrDefault();
                diskDrive?.SetD64DiskImage(d64DiskImage);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading disk image: {ex.Message}");
            }
        }
    }

    private async Task LoadPreloadedDiskImage()
    {
        string selectedPreloadedDisk = SelectedPreloadedDisk;
        if (string.IsNullOrEmpty(selectedPreloadedDisk) || !_preloadedD64Images.ContainsKey(selectedPreloadedDisk))
            return;

        var diskInfo = _preloadedD64Images[selectedPreloadedDisk];
        _isLoadingPreloadedDisk = true;
        _latestPreloadedDiskError = "";

        _logger.LogInformation($"Starting to load preloaded disk: {diskInfo.DisplayName}");

        try
        {
            // Initialize D64AutoDownloadAndRun if not already done
            if (_d64AutoDownloadAndRun == null)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var c64HostConfig = HostApp!.CurrentHostSystemConfig as C64HostConfig;
                _d64AutoDownloadAndRun = new D64AutoDownloadAndRun(
                   _loggerFactory,
                   _httpClient,
                   HostApp!,
                    corsProxyUrl: PlatformDetection.IsRunningInWebAssembly() ? c64HostConfig.CorsProxyURL : null);
            }

            await _d64AutoDownloadAndRun.DownloadAndRunDiskImage(
                diskInfo,
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
            _logger.LogError($"LoadPreloadedDisk_Click error: {_latestPreloadedDiskError}");
        }
        finally
        {
            // Force binding refresh for all properties, config settings for keyboard/joystick may have changed
            RefreshAllBindings();

            _isLoadingPreloadedDisk = false;
            _logger.LogInformation($"Finished loading preloaded disk. Loading state: {_isLoadingPreloadedDisk}");
            if (!string.IsNullOrEmpty(_latestPreloadedDiskError))
            {
                _logger.LogInformation($"Final error state: {_latestPreloadedDiskError}");
            }
        }
    }

    private async Task LoadAssemblyExample()
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

            _logger.LogInformation($"Assembly example loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
            _logger.LogInformation($"Program Counter set to {loadedAtAddress.ToHex()}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading assembly example: {ex.Message}");
            throw;
        }
        finally
        {
            if (wasRunning)
                await HostApp.Start();
        }
    }

    private async Task LoadBasicExample()
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
                _logger.LogWarning($"Loaded program is not a Basic program, it's expected to load at {C64.BASIC_LOAD_ADDRESS.ToHex()} but was loaded at {loadedAtAddress.ToHex()}");
            }
            else
            {
                // Init C64 BASIC memory variables
                c64.InitBasicMemoryVariables(loadedAtAddress, fileLength);
            }

            // Send "list" + NewLine (Return) to the keyboard buffer to immediately list the loaded program
            c64.TextPaste.Paste("list\n");

            _logger.LogInformation($"Basic example loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading basic example: {ex.Message}");
        }
        finally
        {
            if (wasRunning)
                await HostApp.Start();
        }
    }

    private async Task LoadBasicFile(byte[] fileBuffer)
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
                _logger.LogWarning($"Loaded program is not a Basic program, it's expected to load at {C64.BASIC_LOAD_ADDRESS.ToHex()} but was loaded at {loadedAtAddress.ToHex()}");
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

    private async Task LoadBinaryFile(byte[] fileBuffer)
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

            _logger.LogInformation($"Binary program loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
            _logger.LogInformation($"Program Counter set to {loadedAtAddress.ToHex()}");

            await HostApp.Start();
        }
        finally
        {
            if (wasRunning && HostApp.EmulatorState != Systems.EmulatorState.Running)
                await HostApp.Start();
        }
    }

    /// <summary>
    /// Handle KeyDown events from AvaloniaHostApp
    /// </summary>
    private void OnHostKeyDown(object? sender, HostKeyEventArgs e)
    {
        // Check for F9 key to toggle coding assistant
        if (e.Key == Key.F9 && EmulatorState == EmulatorState.Running)
        {
            var toggledAssistantState = !BasicCodingAssistantEnabled;
            BasicCodingAssistantEnabled = toggledAssistantState;
        }
    }

    /// <summary>
    /// Handle KeyUp events from AvaloniaHostApp
    /// </summary>
    private void OnHostKeyUp(object? sender, HostKeyEventArgs e)
    {
        // Currently no special handling for KeyUp events
    }
}
