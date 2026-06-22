using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.Crt;
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.Sharing;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.ViewModels;

public class C64MenuViewModel : ViewModelBase, ISystemMenuContributor
{
    private readonly AvaloniaHostApp _avaloniaHostApp;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public AvaloniaHostApp HostApp => _avaloniaHostApp;

    private readonly Assembly _examplesAssembly = typeof(AvaloniaHostApp).Assembly;
    private string? ExampleFileAssemblyName => _examplesAssembly.GetName().Name;

    // Fields for preloaded program loading functionality
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<string, C64DownloadProgramInfo> _preloadedPrograms = new()
    {
        {"bubblebobble", new C64DownloadProgramInfo("Bubble Bobble", "https://csdb.dk/release/download.php?id=191127", downloadType: C64DownloadProgramType.D64Zip, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: true, directLoadPRGName: "*")}, // Note: Bubble Bobble is not a bitmap game, but somehow this version fails to initialize the custom charset in text mode correctly in SkiaSharp renderer.
        {"compunetreborn", new C64DownloadProgramInfo("Compunet Reborn", "https://compunet.live/static/compunet-reborn-live.prg", downloadType: C64DownloadProgramType.Prg, availableInBrowser: true, c64Variant: "C64PAL", swiftLinkEnabled: true)},
        {"digiloi", new C64DownloadProgramInfo("Digiloi", "https://csdb.dk/release/download.php?id=213381", keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, audioEnabled: true, directLoadPRGName: "*")},
        {"elite", new C64DownloadProgramInfo("Elite", "https://csdb.dk/release/download.php?id=70413", downloadType: C64DownloadProgramType.D64Zip, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: true,directLoadPRGName: "*", c64Variant: "C64PAL")},
        {"gianasisters", new C64DownloadProgramInfo("Giana Sisters", "https://csdb.dk/release/download.php?id=161456", downloadType: C64DownloadProgramType.D64Zip, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: true, directLoadPRGName: "*")},
        {"lastninja", new C64DownloadProgramInfo("Last Ninja", "https://csdb.dk/release/download.php?id=101848", downloadType: C64DownloadProgramType.D64Zip, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: true, directLoadPRGName: "*")},
        {"minizork", new C64DownloadProgramInfo("Mini Zork", "https://csdb.dk/release/download.php?id=42919", audioEnabled: false, directLoadPRGName: "*")},
        {"montezuma", new C64DownloadProgramInfo("Montezuma's Revenge", "https://csdb.dk/release/download.php?id=128101", downloadType: C64DownloadProgramType.D64Zip, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, audioEnabled: true, directLoadPRGName: "*")},
        {"rallyspeedway", new C64DownloadProgramInfo("Rally Speedway", "https://csdb.dk/release/download.php?id=219614", keyboardJoystickEnabled: true, keyboardJoystickNumber: 1, audioEnabled: true, directLoadPRGName: "*")},
    };
    private string _latestPreloadedProgramError = string.Empty;
    private bool _isLoadingPreloadedProgram;
    private C64AutoLoadAndRun? _c64AutoLoadAndRun;

    // --- ReactiveUI Commands ---
    public ReactiveCommand<Unit, Unit> CopyBasicSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteTextCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleDiskImageCommand { get; }
    public ReactiveCommand<Unit, Unit> AttachCartridgeImageCommand { get; }
    public ReactiveCommand<Unit, Unit> DetachCartridgeCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadPreloadedProgramCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadAssemblyExampleCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadBasicExampleCommand { get; }
    public ReactiveCommand<byte[], Unit> LoadBasicFileCommand { get; }
    public ReactiveCommand<Unit, byte[]> SaveBasicFileCommand { get; }
    public ReactiveCommand<byte[], Unit> LoadBinaryFileCommand { get; }

    // Section toggle / joystick commands used by both the UI click handlers and the menu/shortcut bridge.
    public ReactiveCommand<Unit, Unit> ToggleDiskSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCartridgeSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleLoadSaveSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleConfigSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyShareLinkCommand { get; }
    public ReactiveCommand<int, Unit> SetActiveJoystickCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleJoystickKeyboardCommand { get; }
    public ReactiveCommand<int, Unit> SetKeyboardJoystickCommand { get; }
    // --- End ReactiveUI Commands ---

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ReactiveUI WhenAnyValue is used intentionally for ViewModel bindings; members are rooted by XAML and direct references.")]
    public C64MenuViewModel(
        AvaloniaHostApp avaloniaHostApp,
        ILoggerFactory loggerFactory)
    {
        _avaloniaHostApp = avaloniaHostApp ?? throw new ArgumentNullException(nameof(avaloniaHostApp));
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(C64MenuViewModel).Name);

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
                RaiseCartridgePropertiesChanged();
                this.RaisePropertyChanged(nameof(BasicCodingAssistantAvailable));
                this.RaisePropertyChanged(nameof(BasicCodingAssistantEnabled));
                this.RaisePropertyChanged(nameof(IsFileOperationEnabled));
                RecomputeShareLink();
            });

        // Subscribe to KeyDown events from AvaloniaHostApp
        _avaloniaHostApp.KeyDownEvent += OnHostKeyDown;
        _avaloniaHostApp.KeyUpEvent += OnHostKeyUp;

        // Initialize ReactiveCommands
        CopyBasicSourceCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await CopyBasicSourceCodeAsync(),
            this.WhenAnyValue(x => x.IsCopyPasteEnabled),
            RxSchedulers.MainThreadScheduler);

        PasteTextCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await PasteTextInternalAsync(),
            this.WhenAnyValue(x => x.IsCopyPasteEnabled),
            RxSchedulers.MainThreadScheduler);

        ToggleDiskImageCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await ToggleDiskImageInternalAsync(),
            this.WhenAnyValue(x => x.CanToggleDisk),
            RxSchedulers.MainThreadScheduler);

        var canChangeCartridge = _avaloniaHostApp
            .WhenAnyValue(x => x.EmulatorState)
            .CombineLatest(
                this.WhenAnyValue(x => x.IsCartridgeOperationInProgress),
                (state, inProgress) => state != EmulatorState.Uninitialized && !inProgress);

        AttachCartridgeImageCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await AttachCartridgeImageInternalAsync(),
            canChangeCartridge,
            RxSchedulers.MainThreadScheduler);

        DetachCartridgeCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await DetachCartridgeInternalAsync(),
            canChangeCartridge,
            RxSchedulers.MainThreadScheduler);

        LoadPreloadedProgramCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await LoadPreloadedProgramAsync(),
            this.WhenAnyValue(x => x.IsLoadingPreloadedProgram).Select(isLoading => !isLoading),
            RxSchedulers.MainThreadScheduler);

        LoadAssemblyExampleCommand = ReactiveCommandHelper.CreateSafeCommand(
             async () => await LoadAssemblyExampleAsync(),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxSchedulers.MainThreadScheduler);

        LoadBasicExampleCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await LoadBasicExampleAsync(),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxSchedulers.MainThreadScheduler);

        LoadBasicFileCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            async (fileBuffer) => await LoadBasicFileAsync(fileBuffer),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxSchedulers.MainThreadScheduler);

        SaveBasicFileCommand = ReactiveCommandHelper.CreateSafeCommandWithResult<byte[]>(
            async () => await GetBasicProgramAsPrgFileBytesAsync(),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxSchedulers.MainThreadScheduler);

        LoadBinaryFileCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            async (fileBuffer) => await LoadBinaryFileAsync(fileBuffer),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxSchedulers.MainThreadScheduler);

        ToggleDiskSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => ToggleSection(C64MenuSection.Disk),
            null,
            RxSchedulers.MainThreadScheduler);

        ToggleCartridgeSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => ToggleSection(C64MenuSection.Cartridge),
            null,
            RxSchedulers.MainThreadScheduler);

        ToggleLoadSaveSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => ToggleSection(C64MenuSection.LoadSave),
            null,
            RxSchedulers.MainThreadScheduler);

        ToggleConfigSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => ToggleSection(C64MenuSection.Config),
            null,
            RxSchedulers.MainThreadScheduler);

        CopyShareLinkCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await CopyShareLinkAsync(),
            this.WhenAnyValue(x => x.CanCopyShareLink),
            RxSchedulers.MainThreadScheduler);

        SetActiveJoystickCommand = ReactiveCommandHelper.CreateSafeCommand<int>(
            port => CurrentJoystick = port,
            null,
            RxSchedulers.MainThreadScheduler);

        ToggleJoystickKeyboardCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => JoystickKeyboardEnabled = !JoystickKeyboardEnabled,
            null,
            RxSchedulers.MainThreadScheduler);

        SetKeyboardJoystickCommand = ReactiveCommandHelper.CreateSafeCommand<int>(
            port =>
            {
                if (IsKeyboardJoystickSelectionEnabled)
                    KeyboardJoystick = port;
            },
            null,
            RxSchedulers.MainThreadScheduler);
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
                ? ((C64InputHandler)HostApp.CurrentSystemRunner!.System.InputConsumer!).CodingAssistantEnabled : false;

        }
        set
        {
            ((C64InputHandler)HostApp.CurrentSystemRunner!.System.InputConsumer!).CodingAssistantEnabled = value;
            if (C64HostConfig != null)
            {
                C64HostConfig.BasicAIAssistantDefaultEnabled = value;
            }
            this.RaisePropertyChanged(nameof(BasicCodingAssistantEnabled));
        }
    }

    public bool BasicCodingAssistantAvailable => EmulatorState == EmulatorState.Running &&
            HostApp.CurrentRunningSystem is C64 &&
            ((C64InputHandler)HostApp.CurrentSystemRunner!.System.InputConsumer!).CodingAssistantAvailable;

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

    public bool IsCartridgeAttached
        => _avaloniaHostApp.CurrentRunningSystem is C64 c64 &&
           c64.CartridgeSlot.AttachedCartridge != null;

    public string CartridgeAttachButtonText
        => IsCartridgeAttached ? "Replace cartridge..." : "Attach .crt cartridge image";

    public string CartridgeSummary
    {
        get
        {
            if (_avaloniaHostApp.CurrentRunningSystem is not C64 c64 ||
                c64.CartridgeSlot.AttachedCartridge is not { } cartridge)
                return "No cartridge attached";

            var image = c64.AttachedCartridgeImage;
            if (image == null)
                return cartridge.Name;

            var mode = image.Lines switch
            {
                { GameHigh: true, ExromHigh: false } => "generic 8K",
                { GameHigh: false, ExromHigh: false } => "generic 16K",
                { GameHigh: false, ExromHigh: true } => "Ultimax",
                _ => $"hardware type {image.HardwareType}",
            };
            var source = string.IsNullOrWhiteSpace(image.SourceName) ? string.Empty : $" ({image.SourceName})";
            return $"{image.CartridgeName} — {mode}{source}";
        }
    }

    private bool _isCartridgeOperationInProgress;
    public bool IsCartridgeOperationInProgress
    {
        get => _isCartridgeOperationInProgress;
        private set => this.RaiseAndSetIfChanged(ref _isCartridgeOperationInProgress, value);
    }

    private string _latestCartridgeError = string.Empty;
    public string LatestCartridgeError
    {
        get => _latestCartridgeError;
        private set
        {
            this.RaiseAndSetIfChanged(ref _latestCartridgeError, value);
            this.RaisePropertyChanged(nameof(HasLatestCartridgeError));
        }
    }
    public bool HasLatestCartridgeError => !string.IsNullOrWhiteSpace(LatestCartridgeError);

    // Preloaded downloadable programs
    public ObservableCollection<KeyValuePair<string, string>> PreloadedPrograms { get; } = new();
    private string _selectedPreloadedDisk = "";
    public string SelectedPreloadedDisk
    {
        get => _selectedPreloadedDisk;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPreloadedDisk, value);
            LatestPreloadedProgramError = string.Empty;
            RecomputeShareLink();
        }
    }
    public bool IsLoadingPreloadedProgram
    {
        get => _isLoadingPreloadedProgram;
        private set => this.RaiseAndSetIfChanged(ref _isLoadingPreloadedProgram, value);
    }

    public string LatestPreloadedProgramError
    {
        get => _latestPreloadedProgramError;
        private set
        {
            if (_latestPreloadedProgramError == value)
                return;

            this.RaiseAndSetIfChanged(ref _latestPreloadedProgramError, value);
            this.RaisePropertyChanged(nameof(HasLatestPreloadedProgramError));
        }
    }

    public bool HasLatestPreloadedProgramError => !string.IsNullOrEmpty(LatestPreloadedProgramError);

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

    // Section expansion state — bound from XAML so both UI clicks and keyboard shortcuts
    // go through the same ViewModel state.
    private bool _isDiskSectionExpanded = true;
    public bool IsDiskSectionExpanded
    {
        get => _isDiskSectionExpanded;
        private set
        {
            if (_isDiskSectionExpanded == value)
                return;
            _isDiskSectionExpanded = value;
            this.RaisePropertyChanged(nameof(IsDiskSectionExpanded));
            this.RaisePropertyChanged(nameof(DiskSectionHeaderText));
        }
    }

    private bool _isCartridgeSectionExpanded;
    public bool IsCartridgeSectionExpanded
    {
        get => _isCartridgeSectionExpanded;
        private set
        {
            if (_isCartridgeSectionExpanded == value)
                return;
            _isCartridgeSectionExpanded = value;
            this.RaisePropertyChanged(nameof(IsCartridgeSectionExpanded));
        }
    }

    private bool _isLoadSaveSectionExpanded = false;
    public bool IsLoadSaveSectionExpanded
    {
        get => _isLoadSaveSectionExpanded;
        private set
        {
            if (_isLoadSaveSectionExpanded == value)
                return;
            _isLoadSaveSectionExpanded = value;
            this.RaisePropertyChanged(nameof(IsLoadSaveSectionExpanded));
            this.RaisePropertyChanged(nameof(LoadSaveSectionHeaderText));
        }
    }

    private bool _isConfigSectionExpanded = false;
    public bool IsConfigSectionExpanded
    {
        get => _isConfigSectionExpanded;
        private set
        {
            if (_isConfigSectionExpanded == value)
                return;
            _isConfigSectionExpanded = value;
            this.RaisePropertyChanged(nameof(IsConfigSectionExpanded));
            this.RaisePropertyChanged(nameof(ConfigSectionHeaderText));
        }
    }

    public string DiskSectionHeaderText => "Disk Drive & .D64 images";
    public string LoadSaveSectionHeaderText => "Load/Save";
    public string ConfigSectionHeaderText => "Configuration";

    private enum C64MenuSection { Disk, Cartridge, LoadSave, Config }

    private void ToggleSection(C64MenuSection section)
    {
        bool newState = section switch
        {
            C64MenuSection.Disk => !IsDiskSectionExpanded,
            C64MenuSection.Cartridge => !IsCartridgeSectionExpanded,
            C64MenuSection.LoadSave => !IsLoadSaveSectionExpanded,
            C64MenuSection.Config => !IsConfigSectionExpanded,
            _ => false,
        };

        SetSectionExpanded(section, newState, collapseOthers: newState);
    }

    private void SetSectionExpanded(C64MenuSection section, bool expanded, bool collapseOthers)
    {
        switch (section)
        {
            case C64MenuSection.Disk:
                IsDiskSectionExpanded = expanded;
                if (collapseOthers && expanded)
                {
                    IsLoadSaveSectionExpanded = false;
                    IsConfigSectionExpanded = false;
                    IsCartridgeSectionExpanded = false;
                }
                break;
            case C64MenuSection.Cartridge:
                IsCartridgeSectionExpanded = expanded;
                if (collapseOthers && expanded)
                {
                    IsDiskSectionExpanded = false;
                    IsLoadSaveSectionExpanded = false;
                    IsConfigSectionExpanded = false;
                }
                break;
            case C64MenuSection.LoadSave:
                IsLoadSaveSectionExpanded = expanded;
                if (collapseOthers && expanded)
                {
                    IsDiskSectionExpanded = false;
                    IsConfigSectionExpanded = false;
                    IsCartridgeSectionExpanded = false;
                }
                break;
            case C64MenuSection.Config:
                IsConfigSectionExpanded = expanded;
                if (collapseOthers && expanded)
                {
                    IsDiskSectionExpanded = false;
                    IsLoadSaveSectionExpanded = false;
                    IsCartridgeSectionExpanded = false;
                }
                break;
        }
    }

    /// <summary>
    /// Called by the View when validation errors are present: collapse Disk/LoadSave and expand Config.
    /// </summary>
    public void ExpandConfigSectionOnValidationError()
    {
        IsDiskSectionExpanded = false;
        IsCartridgeSectionExpanded = false;
        IsLoadSaveSectionExpanded = false;
        IsConfigSectionExpanded = true;
    }

    // ===== Share link =====
    // URL-length thresholds (total generated URL chars). The binding limit is the host/CDN that
    // serves the app, not the browser; ~8 KB is universally safe, past ~16 KB refuse. See the
    // 'avalonia-browser-share-startup-link' design doc.
    public const int ShareUrlWarnThreshold = 8000;
    public const int ShareUrlRefuseThreshold = 16000;

    /// <summary>Rebuilds the share link from current state. Call before showing the share overlay.</summary>
    public void RefreshShareLink() => RecomputeShareLink();

    /// <summary>Sharing is browser-only and needs the app's base URL (set by the browser host).</summary>
    public bool IsShareFeatureAvailable =>
        PlatformDetection.IsRunningInWebAssembly() && !string.IsNullOrEmpty(_avaloniaHostApp.GetShareBaseUrl());

    private C64ShareMode _shareMode = C64ShareMode.CurrentBasic;
    public bool IsShareModeCurrentBasic
    {
        get => _shareMode == C64ShareMode.CurrentBasic;
        set
        {
            if (!value || _shareMode == C64ShareMode.CurrentBasic)
                return;
            _shareMode = C64ShareMode.CurrentBasic;
            this.RaisePropertyChanged(nameof(IsShareModeCurrentBasic));
            this.RaisePropertyChanged(nameof(IsShareModeDownloadProgram));
            RecomputeShareLink();
        }
    }
    public bool IsShareModeDownloadProgram
    {
        get => _shareMode == C64ShareMode.DownloadProgram;
        set
        {
            if (!value || _shareMode == C64ShareMode.DownloadProgram)
                return;
            _shareMode = C64ShareMode.DownloadProgram;
            this.RaisePropertyChanged(nameof(IsShareModeCurrentBasic));
            this.RaisePropertyChanged(nameof(IsShareModeDownloadProgram));
            RecomputeShareLink();
        }
    }

    private bool _shareAutoRun = true;
    public bool ShareAutoRun
    {
        get => _shareAutoRun;
        set { this.RaiseAndSetIfChanged(ref _shareAutoRun, value); RecomputeShareLink(); }
    }

    private bool _shareIncludeSettings = true;
    public bool ShareIncludeSettings
    {
        get => _shareIncludeSettings;
        set { this.RaiseAndSetIfChanged(ref _shareIncludeSettings, value); RecomputeShareLink(); }
    }

    private string _generatedShareUrl = string.Empty;
    public string GeneratedShareUrl
    {
        get => _generatedShareUrl;
        private set => this.RaiseAndSetIfChanged(ref _generatedShareUrl, value);
    }

    private string _shareUnavailableReason = string.Empty;
    public string ShareUnavailableReason
    {
        get => _shareUnavailableReason;
        private set
        {
            this.RaiseAndSetIfChanged(ref _shareUnavailableReason, value);
            this.RaisePropertyChanged(nameof(HasShareUnavailableReason));
        }
    }
    public bool HasShareUnavailableReason => !string.IsNullOrEmpty(ShareUnavailableReason);

    private string _shareWarning = string.Empty;
    public string ShareWarning
    {
        get => _shareWarning;
        private set
        {
            this.RaiseAndSetIfChanged(ref _shareWarning, value);
            this.RaisePropertyChanged(nameof(HasShareWarning));
        }
    }
    public bool HasShareWarning => !string.IsNullOrEmpty(ShareWarning);

    private bool _isShareUrlTooLong;
    public bool IsShareUrlTooLong
    {
        get => _isShareUrlTooLong;
        private set => this.RaiseAndSetIfChanged(ref _isShareUrlTooLong, value);
    }

    private bool _canCopyShareLink;
    public bool CanCopyShareLink
    {
        get => _canCopyShareLink;
        private set => this.RaiseAndSetIfChanged(ref _canCopyShareLink, value);
    }

    /// <summary>
    /// Rebuilds <see cref="GeneratedShareUrl"/> (and the warning/availability flags) from the current
    /// mode, options and live emulator state. Safe to call on desktop (no-op via the availability guard).
    /// </summary>
    private void RecomputeShareLink()
    {
        if (!IsShareFeatureAvailable)
        {
            ApplyShareUnavailable(reason: string.Empty);
            return;
        }

        var (request, unavailableReason) = BuildShareRequest();
        if (request is null)
        {
            ApplyShareUnavailable(unavailableReason ?? string.Empty);
            return;
        }

        string url;
        try
        {
            url = C64ShareLinkBuilder.Build(_avaloniaHostApp.GetShareBaseUrl()!, request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build C64 share link.");
            ApplyShareUnavailable("Could not generate a share link from the current state.");
            return;
        }

        ShareUnavailableReason = string.Empty;
        GeneratedShareUrl = url;

        var length = url.Length;
        if (length > ShareUrlRefuseThreshold)
        {
            IsShareUrlTooLong = true;
            ShareWarning = $"This link is too long to share reliably ({length:N0} characters). Try a shorter BASIC program.";
            CanCopyShareLink = false;
        }
        else if (length > ShareUrlWarnThreshold)
        {
            IsShareUrlTooLong = false;
            ShareWarning = $"This link is long ({length:N0} characters) and may not open on some hosts.";
            CanCopyShareLink = true;
        }
        else
        {
            IsShareUrlTooLong = false;
            ShareWarning = string.Empty;
            CanCopyShareLink = true;
        }
    }

    private void ApplyShareUnavailable(string reason)
    {
        GeneratedShareUrl = string.Empty;
        ShareUnavailableReason = reason;
        ShareWarning = string.Empty;
        IsShareUrlTooLong = false;
        CanCopyShareLink = false;
    }

    private (C64ShareLinkRequest? request, string? unavailableReason) BuildShareRequest()
        => _shareMode switch
        {
            C64ShareMode.CurrentBasic => BuildCurrentBasicShareRequest(),
            C64ShareMode.DownloadProgram => BuildDownloadProgramShareRequest(),
            _ => (null, null),
        };

    private (C64ShareLinkRequest?, string?) BuildCurrentBasicShareRequest()
    {
        if (EmulatorState != EmulatorState.Running || !IsC64System())
            return (null, "Start the C64 to share its current BASIC program.");

        string basicText;
        try
        {
            var c64 = (C64)_avaloniaHostApp.CurrentRunningSystem!;
            basicText = c64.BasicTokenParser.GetBasicText().ToLower();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read C64 BASIC source for share link.");
            return (null, "Could not read the current BASIC program.");
        }

        if (string.IsNullOrWhiteSpace(basicText))
            return (null, "There is no BASIC program to share.");

        var c64HostConfig = C64HostConfig;
        return (new C64ShareLinkRequest
        {
            Mode = C64ShareMode.CurrentBasic,
            SystemVariant = _avaloniaHostApp.SelectedSystemConfigurationVariant,
            AutoRun = ShareAutoRun,
            BasicText = basicText,
            IncludeSettings = ShareIncludeSettings,
            AudioEnabled = c64HostConfig?.SystemConfig?.AudioEnabled ?? false,
            KeyboardJoystickEnabled = c64HostConfig?.SystemConfig?.KeyboardJoystickEnabled ?? false,
            KeyboardJoystickNumber = c64HostConfig?.SystemConfig?.KeyboardJoystick ?? 2,
        }, null);
    }

    private (C64ShareLinkRequest?, string?) BuildDownloadProgramShareRequest()
    {
        var key = SelectedPreloadedDisk;
        if (string.IsNullOrEmpty(key) || !_preloadedPrograms.TryGetValue(key, out var info))
            return (null, "Select a program under 'Disk Drive & .D64 images → Download & Run' to share it.");

        // A downloadable program carries its own recommended variant/settings (applied by the
        // Download & Run flow), so the shared link uses those rather than the current live config.
        return (new C64ShareLinkRequest
        {
            Mode = C64ShareMode.DownloadProgram,
            SystemVariant = string.IsNullOrEmpty(info.C64Variant)
                ? _avaloniaHostApp.SelectedSystemConfigurationVariant
                : info.C64Variant,
            AutoRun = ShareAutoRun,
            DownloadUrl = info.DownloadUrl,
            DownloadType = info.DownloadType,
            DirectLoadPRGName = info.DirectLoadPRGName,
            IncludeSettings = ShareIncludeSettings,
            AudioEnabled = info.AudioEnabled,
            KeyboardJoystickEnabled = info.KeyboardJoystickEnabled,
            KeyboardJoystickNumber = info.KeyboardJoystickNumber,
        }, null);
    }

    private async Task CopyShareLinkAsync()
    {
        if (!CanCopyShareLink || string.IsNullOrEmpty(GeneratedShareUrl))
            return;
        await RequestClipboardCopyAsync(GeneratedShareUrl);
    }

    // --- ISystemMenuContributor ---
    public string MenuLabel => "C64";

    public IReadOnlyList<NativeMenuItemBase> GetNativeMenuItems()
    {
        // On macOS, NativeMenu items appear in the OS-level system menu bar (not the app window),
        // which is the desired UX. The menu bar is also exposed via the macOS Accessibility API,
        // making shortcuts self-describing and discoverable by AI agents at runtime.
        // Use Meta+Alt (⌘⌥) as the primary modifier so hints show as "⌘⌥L" etc.
        const KeyModifiers macBase = KeyModifiers.Meta | KeyModifiers.Alt;
        //const KeyModifiers macBase = KeyModifiers.Alt;
        const KeyModifiers macShift = KeyModifiers.Meta | KeyModifiers.Alt | KeyModifiers.Shift;

        return new NativeMenuItemBase[]
        {
            BuildMenuItem("Toggle Disk Drive section", new KeyGesture(Key.D, macShift), ToggleDiskSectionCommand),
            BuildMenuItem("Toggle Load/Save section", new KeyGesture(Key.L, macShift), ToggleLoadSaveSectionCommand),
            BuildMenuItem("Toggle Configuration section", new KeyGesture(Key.C, macShift), ToggleConfigSectionCommand),
            new NativeMenuItemSeparator(),
            BuildMenuItem("Active joystick: Port 1", new KeyGesture(Key.D1, macBase), SetActiveJoystickCommand, 1),
            BuildMenuItem("Active joystick: Port 2", new KeyGesture(Key.D2, macBase), SetActiveJoystickCommand, 2),
            new NativeMenuItemSeparator(),
            BuildMenuItem("Toggle Joystick KB", new KeyGesture(Key.K, macBase), ToggleJoystickKeyboardCommand),
            BuildMenuItem("Keyboard joystick: Port 1", new KeyGesture(Key.D1, macShift), SetKeyboardJoystickCommand, 1),
            BuildMenuItem("Keyboard joystick: Port 2", new KeyGesture(Key.D2, macShift), SetKeyboardJoystickCommand, 2),
        };
    }

    public IReadOnlyList<KeyBinding> GetKeyBindings()
    {
        // On Windows/Linux, NativeMenu would render as in-window chrome, which is not the desired
        // UX (on macOS it goes to the OS system menu bar, which is fine there). KeyBindings are
        // used instead: registered on the main Window, they fire regardless of which child has focus.
        // Ctrl+Alt combos are safe alongside the C64 emulator's own Ctrl+key color combinations
        // (those trigger on plain Ctrl, without Alt).
        const KeyModifiers nonMacBase = KeyModifiers.Control | KeyModifiers.Alt;
        const KeyModifiers nonMacShift = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;

        return new[]
        {
            BuildKeyBinding(new KeyGesture(Key.D, nonMacShift), ToggleDiskSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.L, nonMacShift), ToggleLoadSaveSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.C, nonMacShift), ToggleConfigSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.D1, nonMacBase), SetActiveJoystickCommand, 1),
            BuildKeyBinding(new KeyGesture(Key.D2, nonMacBase), SetActiveJoystickCommand, 2),
            BuildKeyBinding(new KeyGesture(Key.K, nonMacBase), ToggleJoystickKeyboardCommand),
            BuildKeyBinding(new KeyGesture(Key.D1, nonMacShift), SetKeyboardJoystickCommand, 1),
            BuildKeyBinding(new KeyGesture(Key.D2, nonMacShift), SetKeyboardJoystickCommand, 2),
        };
    }

    private static NativeMenuItem BuildMenuItem(string header, KeyGesture gesture, System.Windows.Input.ICommand command, object? parameter = null)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            Gesture = gesture,
            Command = command,
        };
        if (parameter != null)
            item.CommandParameter = parameter;
        return item;
    }

    private static KeyBinding BuildKeyBinding(KeyGesture gesture, System.Windows.Input.ICommand command, object? parameter = null)
    {
        var binding = new KeyBinding
        {
            Gesture = gesture,
            Command = command,
        };
        if (parameter != null)
            binding.CommandParameter = parameter;
        return binding;
    }

    private void InitializeC64Data()
    {
        // Initialize joystick options
        AvailableJoysticks.Clear();
        AvailableJoysticks.Add(1);
        AvailableJoysticks.Add(2);

        // Initialize preloaded downloadable programs
        PreloadedPrograms.Clear();
        PreloadedPrograms.Add(new KeyValuePair<string, string>("", "-- Select a program --"));
        var preloadedPrograms = PlatformDetection.IsRunningInWebAssembly()
            ? _preloadedPrograms.Where(d => d.Value.AvailableInBrowser)
            : _preloadedPrograms.AsEnumerable();
        foreach (var disk in preloadedPrograms.OrderBy(d => d.Value.DisplayName))
        {
            PreloadedPrograms.Add(new KeyValuePair<string, string>(disk.Key, disk.Value.DisplayName));
        }

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
    private async Task CopyBasicSourceCodeAsync()
    {
        if (_avaloniaHostApp?.EmulatorState != EmulatorState.Running ||
          !IsC64System())
            return;

        try
        {
            var c64 = (C64)_avaloniaHostApp.CurrentRunningSystem!;
            var sourceCode = c64.BasicTokenParser.GetBasicText();

            // Request View to copy to clipboard
            await RequestClipboardCopyAsync(sourceCode.ToLower());
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error copying Basic source: {ex.Message}");
        }
    }

    private async Task PasteTextInternalAsync()
    {
        var avaloniaHostApp = _avaloniaHostApp;
        if (avaloniaHostApp == null || avaloniaHostApp.EmulatorState != EmulatorState.Running || !IsC64System())
            return;

        try
        {
            // Request text from View's clipboard
            var text = await RequestClipboardPasteAsync();
            if (!string.IsNullOrEmpty(text))
            {
                var c64 = (C64)avaloniaHostApp.CurrentRunningSystem!;
                c64.TextPaste.Paste(text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error pasting text: {ex.Message}");
        }
    }

    private async Task ToggleDiskImageInternalAsync()
    {
        var avaloniaHostApp = _avaloniaHostApp;
        if (avaloniaHostApp == null || avaloniaHostApp.EmulatorState == EmulatorState.Uninitialized || !IsC64System())
            return;

        try
        {
            if (avaloniaHostApp.CurrentRunningSystem is not C64 c64)
                return;

            var diskDrive = c64.IECBus?.Devices?.OfType<Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541>().FirstOrDefault();

            if (diskDrive?.IsDisketteInserted == true)
            {
                // Detach current disk image
                diskDrive.RemoveD64DiskImage();
            }
            else
            {
                // Request View to show file picker and attach disk image
                await RequestAttachDiskImageAsync();
            }

            // Notify that the disk image state has changed
            RefreshAllBindings();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error toggling disk image: {ex.Message}");
        }
    }

    private async Task AttachCartridgeImageInternalAsync()
    {
        if (_avaloniaHostApp.CurrentRunningSystem is not C64 c64)
            return;

        LatestCartridgeError = string.Empty;
        if (c64.CartridgeSlot.AttachedCartridge is { } current)
        {
            var confirmed = await RequestCartridgeReplaceConfirmationAsync(current.Name);
            if (!confirmed)
                return;
        }

        var selectedFile = await RequestAttachCartridgeImageAsync();
        if (selectedFile == null)
            return;

        IsCartridgeOperationInProgress = true;
        var wasRunning = _avaloniaHostApp.EmulatorState == EmulatorState.Running;
        try
        {
            if (wasRunning)
                _avaloniaHostApp.Pause();

            c64.AttachCrtImage(selectedFile.Bytes, selectedFile.Name);
        }
        catch (Exception ex)
        {
            LatestCartridgeError = string.IsNullOrWhiteSpace(ex.Message)
                ? "Could not attach the CRT cartridge image."
                : ex.Message;
            _logger.LogError(ex, "Error attaching CRT cartridge image {FileName}", selectedFile.Name);
        }
        finally
        {
            if (wasRunning && _avaloniaHostApp.EmulatorState == EmulatorState.Paused)
                await _avaloniaHostApp.Start();
            IsCartridgeOperationInProgress = false;
            RaiseCartridgePropertiesChanged();
        }
    }

    private async Task DetachCartridgeInternalAsync()
    {
        if (_avaloniaHostApp.CurrentRunningSystem is not C64 c64 ||
            c64.CartridgeSlot.AttachedCartridge == null)
            return;

        LatestCartridgeError = string.Empty;
        IsCartridgeOperationInProgress = true;
        var wasRunning = _avaloniaHostApp.EmulatorState == EmulatorState.Running;
        try
        {
            if (wasRunning)
                _avaloniaHostApp.Pause();

            c64.DetachCartridgeAndReset();
        }
        catch (Exception ex)
        {
            LatestCartridgeError = string.IsNullOrWhiteSpace(ex.Message)
                ? "Could not detach the cartridge."
                : ex.Message;
            _logger.LogError(ex, "Error detaching C64 cartridge");
        }
        finally
        {
            if (wasRunning && _avaloniaHostApp.EmulatorState == EmulatorState.Paused)
                await _avaloniaHostApp.Start();
            IsCartridgeOperationInProgress = false;
            RaiseCartridgePropertiesChanged();
        }
    }

    private async Task<SelectedBinaryFile?> RequestAttachCartridgeImageAsync()
    {
        if (AttachCartridgeImageRequested == null)
            return null;

        var tcs = new TaskCompletionSource<SelectedBinaryFile?>();
        AttachCartridgeImageRequested.Invoke(this, tcs);
        return await tcs.Task;
    }

    private async Task<bool> RequestCartridgeReplaceConfirmationAsync(string cartridgeName)
    {
        if (ConfirmCartridgeReplaceRequested == null)
            return false;

        var tcs = new TaskCompletionSource<bool>();
        ConfirmCartridgeReplaceRequested.Invoke(
            this,
            new CartridgeReplaceConfirmationEventArgs(cartridgeName, tcs));
        return await tcs.Task;
    }

    private void RaiseCartridgePropertiesChanged()
    {
        this.RaisePropertyChanged(nameof(IsCartridgeAttached));
        this.RaisePropertyChanged(nameof(CartridgeAttachButtonText));
        this.RaisePropertyChanged(nameof(CartridgeSummary));
        this.RaisePropertyChanged(nameof(HasLatestCartridgeError));
    }

    private bool IsC64System()
    {
        return string.Equals(_avaloniaHostApp?.SelectedSystemName, C64.SystemName, StringComparison.OrdinalIgnoreCase);
    }

    // Events for View to handle clipboard and file operations
    public event EventHandler<string>? ClipboardCopyRequested;
    public event EventHandler<TaskCompletionSource<string?>>? ClipboardPasteRequested;
    public event EventHandler<TaskCompletionSource<byte[]?>>? AttachDiskImageRequested;
    public event EventHandler<TaskCompletionSource<SelectedBinaryFile?>>? AttachCartridgeImageRequested;
    public event EventHandler<CartridgeReplaceConfirmationEventArgs>? ConfirmCartridgeReplaceRequested;

    private async Task RequestClipboardCopyAsync(string text)
    {
        ClipboardCopyRequested?.Invoke(this, text);
        await Task.CompletedTask;
    }

    private async Task<string?> RequestClipboardPasteAsync()
    {
        if (ClipboardPasteRequested == null)
            return null;
        var tcs = new TaskCompletionSource<string?>();
        ClipboardPasteRequested.Invoke(this, tcs);
        return await tcs.Task;
    }

    private async Task RequestAttachDiskImageAsync()
    {
        if (AttachDiskImageRequested == null)
            return;

        var tcs = new TaskCompletionSource<byte[]?>();
        AttachDiskImageRequested.Invoke(this, tcs);
        var fileBuffer = await tcs.Task;

        if (fileBuffer != null)
        {
            try
            {
                // Parse the D64 disk image
                var d64DiskImage = Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.D64Parser.ParseD64File(fileBuffer);

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

    private async Task LoadPreloadedProgramAsync()
    {
        var hostApp = HostApp;
        if (hostApp == null)
            return;

        string selectedPreloadedDisk = SelectedPreloadedDisk;
        if (string.IsNullOrEmpty(selectedPreloadedDisk) || !_preloadedPrograms.ContainsKey(selectedPreloadedDisk))
            return;

        var programInfo = _preloadedPrograms[selectedPreloadedDisk];
        IsLoadingPreloadedProgram = true;
        LatestPreloadedProgramError = string.Empty;

        _logger.LogInformation("Starting to load preloaded program: {DisplayName}", programInfo.DisplayName);

        try
        {
            // Initialize C64AutoLoadAndRun if not already done
            if (_c64AutoLoadAndRun == null)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                if (hostApp.CurrentHostSystemConfig is not C64HostConfig _)
                    return;

                _c64AutoLoadAndRun = new C64AutoLoadAndRun(
                   _loggerFactory,
                   _httpClient,
                   hostApp,
                   corsProxyUrl: _avaloniaHostApp.GetCorsProxyUrl());
            }

            await _c64AutoLoadAndRun.DownloadAndRunProgram(
                programInfo,
                setConfigCallback: async (programInfo) =>
                {
                    if (hostApp.CurrentHostSystemConfig is not C64HostConfig c64HostConfig)
                        return;

                    var c64SystemConfig = c64HostConfig.SystemConfig;

                    // Apply keyboard joystick settings to config object while emulator is stopped
                    c64SystemConfig.KeyboardJoystickEnabled = programInfo.KeyboardJoystickEnabled;
                    c64SystemConfig.KeyboardJoystick = programInfo.KeyboardJoystickNumber;

                    // Apply keyboard settings to config object while emulator is stopped (assume joystick should use same as keyboard joystick number)
                    c64HostConfig.InputConfig.CurrentJoystick = programInfo.KeyboardJoystickNumber;

                    // Apply renderer setting to config object while emulator is stopped
                    // TODO: If/when a optimized RenderType for use without bitmap graphics is available, set rendererProviderType appropriately here.
                    //Type rendererProviderType = diskInfo.RequiresBitmap ? typeof(Vic2Rasterizer) : typeof(C64VideoCommandStream);
                    Type rendererProviderType = typeof(Vic2Rasterizer);
                    var renderProviderAndTargetCombinations = hostApp.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations();
                    var compatibleRenderTargetType = renderProviderAndTargetCombinations
                        .Where(c => c.renderProviderType == rendererProviderType)
                        .Select(c => c.renderTargetType)
                        .FirstOrDefault();

                    c64HostConfig.SystemConfig.SetRenderProviderType(rendererProviderType);
                    c64HostConfig.SystemConfig.SetRenderTargetType(compatibleRenderTargetType);

                    // Apply audio enabled setting to config object while emulator is stopped
                    c64SystemConfig.AudioEnabled = programInfo.AudioEnabled;

                    // Apply SwiftLink setting to config object while emulator is stopped
                    c64SystemConfig.SwiftLink.Enabled = programInfo.SwiftLinkEnabled;

                    // Apply C64 variant setting to config object while emulator is stopped
                    await hostApp.SelectSystemConfigurationVariant(programInfo.C64Variant);

                    hostApp.UpdateHostSystemConfig(c64HostConfig);
                });
        }
        catch (Exception ex)
        {
            LatestPreloadedProgramError = string.IsNullOrWhiteSpace(ex.Message)
                ? $"Failed to download and run {programInfo.DisplayName}."
                : ex.Message;
            _logger.LogError(
                ex,
                "LoadPreloadedProgram error while loading {DisplayName}: {ErrorMessage}",
                programInfo.DisplayName,
                LatestPreloadedProgramError);
        }
        finally
        {
            // Force binding refresh for all properties, config settings for keyboard/joystick may have changed
            RefreshAllBindings();

            IsLoadingPreloadedProgram = false;
            _logger.LogInformation("Finished loading preloaded program. Loading state: {IsLoadingPreloadedProgram}", IsLoadingPreloadedProgram);
            if (!string.IsNullOrEmpty(LatestPreloadedProgramError))
            {
                _logger.LogInformation("Final error state: {LatestPreloadedProgramError}", LatestPreloadedProgramError);
            }
        }
    }

    private async Task LoadAssemblyExampleAsync()
    {
        var hostApp = HostApp;
        if (hostApp == null || hostApp.EmulatorState == Systems.EmulatorState.Uninitialized)
            return;

        string? file = SelectedAssemblyExample;
        if (string.IsNullOrEmpty(file))
            return;

        bool wasRunning = hostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();

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
                hostApp.CurrentRunningSystem!.Mem,
                prgBytes,
                out ushort loadedAtAddress,
                out ushort fileLength);

            // Set Program Counter to start of loaded file
            hostApp.CurrentRunningSystem.CPU.PC = loadedAtAddress;

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
                await hostApp.Start();
        }
    }

    private async Task LoadBasicExampleAsync()
    {
        var hostApp = HostApp;
        if (hostApp == null || hostApp.EmulatorState == Systems.EmulatorState.Uninitialized)
            return;

        string? file = SelectedBasicExample;
        if (string.IsNullOrEmpty(file))
            return;

        bool wasRunning = hostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();

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
                hostApp.CurrentRunningSystem!.Mem,
              prgBytes,
              out ushort loadedAtAddress,
                  out ushort fileLength);

            var c64 = (C64)hostApp.CurrentRunningSystem!;
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
                await hostApp.Start();
        }
    }

    private async Task LoadBasicFileAsync(byte[] fileBuffer)
    {
        var hostApp = HostApp;
        if (hostApp == null || hostApp.EmulatorState == Systems.EmulatorState.Uninitialized)
            return;

        bool wasRunning = hostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();

        try
        {
            BinaryLoader.Load(
                hostApp.CurrentRunningSystem!.Mem,
                fileBuffer,
                out ushort loadedAtAddress,
                out ushort fileLength);

            if (loadedAtAddress != C64.BASIC_LOAD_ADDRESS)
            {
                _logger.LogWarning($"Loaded program is not a Basic program, it's expected to load at {C64.BASIC_LOAD_ADDRESS.ToHex()} but was loaded at {loadedAtAddress.ToHex()}");
            }
            else
            {
                var c64 = (C64)hostApp.CurrentRunningSystem!;
                c64.InitBasicMemoryVariables(loadedAtAddress, fileLength);
            }
        }
        finally
        {
            if (wasRunning)
                await hostApp.Start();
        }
    }

    public async Task<byte[]> GetBasicProgramAsPrgFileBytesAsync()
    {
        var hostApp = HostApp;
        if (hostApp == null || hostApp.EmulatorState == Systems.EmulatorState.Uninitialized)
            return Array.Empty<byte>();

        bool wasRunning = hostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();

        try
        {
            ushort startAddress = C64.BASIC_LOAD_ADDRESS;
            var currentRunningSystem = hostApp.CurrentRunningSystem;
            if (currentRunningSystem is not C64 c64)
                return Array.Empty<byte>();

            var endAddress = c64.GetBasicProgramEndAddress();

            var saveData = BinarySaver.BuildSaveData(
                currentRunningSystem.Mem,
                startAddress,
                endAddress,
                addFileHeaderWithLoadAddress: true);

            return saveData;
        }
        finally
        {
            if (wasRunning)
                await hostApp.Start();
        }
    }

    private async Task LoadBinaryFileAsync(byte[] fileBuffer)
    {
        var hostApp = HostApp;
        if (hostApp == null || hostApp.EmulatorState == Systems.EmulatorState.Uninitialized)
            return;

        bool wasRunning = hostApp.EmulatorState == Systems.EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();

        try
        {
            BinaryLoader.Load(
                hostApp.CurrentRunningSystem!.Mem,
                fileBuffer,
                out ushort loadedAtAddress,
                out ushort fileLength);

            hostApp.CurrentRunningSystem.CPU.PC = loadedAtAddress;

            _logger.LogInformation($"Binary program loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
            _logger.LogInformation($"Program Counter set to {loadedAtAddress.ToHex()}");

            await hostApp.Start();
        }
        finally
        {
            if (wasRunning && hostApp.EmulatorState != Systems.EmulatorState.Running)
                await hostApp.Start();
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

public sealed record SelectedBinaryFile(string Name, byte[] Bytes);

public sealed class CartridgeReplaceConfirmationEventArgs : EventArgs
{
    public CartridgeReplaceConfirmationEventArgs(
        string cartridgeName,
        TaskCompletionSource<bool> completion)
    {
        CartridgeName = cartridgeName;
        Completion = completion;
    }

    public string CartridgeName { get; }
    public TaskCompletionSource<bool> Completion { get; }
}
