using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Impl.Avalonia.Apple2;
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
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Apple2.Disk2;
using Highbyte.DotNet6502.Systems.Apple2.DiskImage;
using Highbyte.DotNet6502.Systems.Apple2.DiskImage.Download;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Apple2.ViewModels;

/// <summary>
/// Menu/sidebar contribution for the Apple II shell plugin: the Load/Save section for
/// Applesoft BASIC and binary programs (mirroring the C64 menu), and the Configuration
/// section — which is also the route to the ROM settings dialog.
///
/// File formats differ from the C64's .prg convention:
/// - BASIC files are bare tokenized Applesoft bytes with no header; they always load at
///   <see cref="Apple2System.BASIC_LOAD_ADDRESS"/> ($0801), after which the Applesoft
///   zero-page pointers are initialised so RUN and LIST work.
/// - Binary files use the DOS 3.3 "B" layout: a 4-byte header (load address + length,
///   little endian) followed by the code — the same layout BLOAD/BRUN use.
/// </summary>
public class Apple2MenuViewModel : ViewModelBase, ISystemMenuContributor
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly Assembly _examplesAssembly = typeof(AvaloniaHostApp).Assembly;
    private string? ExampleFileAssemblyName => _examplesAssembly.GetName().Name;

    private enum Apple2MenuSection { Download, Disk, LoadSave, Config }

    // Accordion behavior (like the C64 menu): expanding one section collapses the others.
    private readonly AccordionSections<Apple2MenuSection> _sections;

    private readonly HttpClient _httpClient = new();
    private DskDiskImage? _attachedDiskImage;
    private Apple2AutoLoadAndRun? _apple2AutoLoadAndRun;

    /// <summary>
    /// The curated "Download &amp; Run" list. Each entry says how it runs: RAM-resident programs
    /// are injected into memory, while self-booting titles are booted in the Disk II drive (and
    /// so need the optional <c>disk2</c> ROM).
    ///
    /// Every entry must be verified in the running emulator before inclusion — and prefer a
    /// cracked release for commercial titles. Copy protection is not emulated, so an untouched
    /// original typically reads the disk and then sits on a black screen: the plain
    /// <c>choplifter.dsk</c> and <c>bolo.dsk</c> on the archive do exactly that, while the 4am
    /// cracks of the same games boot fine.
    /// </summary>
    private readonly Dictionary<string, Apple2DownloadProgramInfo> _preloadedPrograms = new()
    {
        { "applepanic", new Apple2DownloadProgramInfo(
            "Apple Panic",
            "https://mirrors.apple2.org.za/ftp.apple.asimov.net/images/games/action/apple_panic/apple_panic.dsk") },

        { "loderunner", new Apple2DownloadProgramInfo(
            "Lode Runner",
            "https://mirrors.apple2.org.za/ftp.apple.asimov.net/images/games/action/lode_runner/Lode%20Runner%20%284am%20crack%29.zip",
            zipEntryName: "Lode Runner (4am crack)/Lode Runner (4am crack).dsk",
            runMode: Apple2DownloadRunMode.BootDisk,
            keyboardJoystickEnabled: true) },

        { "choplifter", new Apple2DownloadProgramInfo(
            "Choplifter",
            "https://mirrors.apple2.org.za/ftp.apple.asimov.net/images/games/action/Choplifter%20%284am%20and%20san%20inc%20crack%29.zip",
            zipEntryName: "Choplifter (4am and san inc crack)/Choplifter (4am and san inc crack).dsk",
            runMode: Apple2DownloadRunMode.BootDisk,
            keyboardJoystickEnabled: true) },

        { "bolo", new Apple2DownloadProgramInfo(
            "Bolo",
            "https://mirrors.apple2.org.za/ftp.apple.asimov.net/images/games/action/Bolo%20%284am%20crack%29.zip",
            zipEntryName: "Bolo (4am crack)/Bolo (4am crack).dsk",
            runMode: Apple2DownloadRunMode.BootDisk) },

        // The first ProDOS title in the list: it boots ProDOS 8 into the language card, and its
        // image is ProDOS-ordered despite the .dsk name (the drive detects the order from the
        // contents). Keyboard joystick stays off — the game reads the keyboard directly and never
        // touches the game port, so switching it on would only steal keys from it.
        { "dangerousdave", new Apple2DownloadProgramInfo(
            "Dangerous Dave",
            "https://archive.org/download/a2_asimov_dangerous_dave/dangerous_dave.dsk",
            runMode: Apple2DownloadRunMode.BootDisk) },

        // The one non-game, and the only entry not on the asimov mirror — that mirror has no
        // VisiCalc image. Booted rather than injected: the binary is a plain catalog file, but
        // VisiCalc's /S and /L commands call DOS, so DOS has to be resident. Booting lands on the
        // disk's own HELLO menu, where 1 starts VisiCalc.
        { "visicalc", new Apple2DownloadProgramInfo(
            "VisiCalc",
            "https://archive.org/download/Visicalc_1.27/Visicalc_1.27.dsk",
            runMode: Apple2DownloadRunMode.BootDisk) },
    };

    public AvaloniaHostApp HostApp => _hostApp;

    public ReactiveCommand<Unit, Unit> ToggleConfigSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleJoystickKeyboardCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleLoadSaveSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleDownloadSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleDiskSectionCommand { get; }
    public ReactiveCommand<byte[], Unit> InsertDiskCommand { get; }
    public ReactiveCommand<Unit, Unit> BootDiskCommand { get; }
    public ReactiveCommand<Unit, Unit> EjectDiskCommand { get; }
    public ReactiveCommand<byte[], Unit> LoadBasicFileCommand { get; }
    public ReactiveCommand<byte[], Unit> LoadBinaryFileCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadAssemblyExampleCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadBasicExampleCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyBasicSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteTextCommand { get; }
    public ReactiveCommand<byte[], Unit> AttachDskImageCommand { get; }
    public ReactiveCommand<Unit, Unit> RunDskFileCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadPreloadedProgramCommand { get; }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ReactiveCommand usage is limited to application-defined view models rooted by the host application.")]
    public Apple2MenuViewModel(AvaloniaHostApp hostApp, ILoggerFactory loggerFactory)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _logger = loggerFactory.CreateLogger(nameof(Apple2MenuViewModel));
        _loggerFactory = loggerFactory;

        _sections = new AccordionSections<Apple2MenuSection>(
            OnSectionStateChanged, initiallyExpanded: Apple2MenuSection.Download);

        _hostApp
            .WhenAnyValue(x => x.EmulatorState)
            .Subscribe(_ => RaiseEmulatorStateChanged());

        // The config dialog's OK replaces the host system config, so the sidebar checkbox has to
        // re-read; without this the two show different values until something else refreshes.
        _hostApp
            .WhenAnyValue(x => x.CurrentHostSystemConfig)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(KeyboardJoystickEnabled)));

        ToggleConfigSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                _sections.Toggle(Apple2MenuSection.Config);
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        ToggleJoystickKeyboardCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                KeyboardJoystickEnabled = !KeyboardJoystickEnabled;
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        ToggleLoadSaveSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                _sections.Toggle(Apple2MenuSection.LoadSave);
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        LoadBasicFileCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            async fileBytes => await LoadBasicFileAsync(fileBytes),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        LoadBinaryFileCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            async fileBytes => await LoadBinaryFileAsync(fileBytes),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        LoadAssemblyExampleCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await LoadAssemblyExampleAsync(),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        LoadBasicExampleCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await LoadBasicExampleAsync(),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        CopyBasicSourceCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await CopyBasicSourceCodeAsync(),
            this.WhenAnyValue(x => x.IsCopyPasteEnabled),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        PasteTextCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await PasteTextInternalAsync(),
            this.WhenAnyValue(x => x.IsCopyPasteEnabled),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        ToggleDownloadSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                _sections.Toggle(Apple2MenuSection.Download);
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        AttachDskImageCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            fileBytes =>
            {
                AttachDskImage(fileBytes);
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        RunDskFileCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await RunDskFileAsync(),
            this.WhenAnyValue(x => x.IsFileOperationEnabled, x => x.SelectedDskFile,
                (enabled, selected) => enabled && !string.IsNullOrEmpty(selected)),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        LoadPreloadedProgramCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await LoadPreloadedProgramAsync(),
            this.WhenAnyValue(x => x.IsLoadingPreloadedProgram).Select(loading => !loading),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        ToggleDiskSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                _sections.Toggle(Apple2MenuSection.Disk);
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        InsertDiskCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            async fileBytes => await InsertDiskAsync(fileBytes),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        BootDiskCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () => await BootDiskAsync(),
            this.WhenAnyValue(x => x.HasInsertedDisk),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        EjectDiskCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                EjectDisk();
                return Task.CompletedTask;
            },
            this.WhenAnyValue(x => x.HasInsertedDisk),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        InitExampleFiles();
        InitPreloadedPrograms();
    }

    public bool IsConfigSectionExpanded => _sections.IsExpanded(Apple2MenuSection.Config);

    public bool IsLoadSaveSectionExpanded => _sections.IsExpanded(Apple2MenuSection.LoadSave);

    public bool IsDownloadSectionExpanded => _sections.IsExpanded(Apple2MenuSection.Download);

    public bool IsDiskSectionExpanded => _sections.IsExpanded(Apple2MenuSection.Disk);

    private void OnSectionStateChanged(Apple2MenuSection section)
    {
        var propertyName = section switch
        {
            Apple2MenuSection.Download => nameof(IsDownloadSectionExpanded),
            Apple2MenuSection.Disk => nameof(IsDiskSectionExpanded),
            Apple2MenuSection.LoadSave => nameof(IsLoadSaveSectionExpanded),
            _ => nameof(IsConfigSectionExpanded),
        };
        this.RaisePropertyChanged(propertyName);
    }

    /// <summary>Configuration may only be edited while the emulator is not running.</summary>
    public bool IsApple2ConfigEnabled => _hostApp.EmulatorState == EmulatorState.Uninitialized;

    /// <summary>
    /// Whether the Apple II configuration is currently unusable — in practice, on a machine where
    /// the ROM files have not been supplied yet, which is what a first run after installing looks
    /// like. The View uses this to expand the Configuration section and draw attention to the
    /// config button, so the one thing standing between the user and a working emulator is not
    /// hidden behind a collapsed section.
    /// </summary>
    public bool HasConfigValidationErrors
        => Apple2HostConfig != null && !Apple2HostConfig.IsValid(out _);

    private Apple2HostConfig? Apple2HostConfig => _hostApp.CurrentHostSystemConfig as Apple2HostConfig;

    /// <summary>
    /// Called by the View when the configuration is invalid: expands Config (which collapses the
    /// other sections, per the accordion).
    /// </summary>
    public void ExpandConfigSectionOnValidationError()
        => _sections.SetExpanded(Apple2MenuSection.Config, true);

    /// <summary>
    /// Re-evaluates every binding on this view model. Used after the configuration dialog closes,
    /// when settings the menu reflects may have changed wholesale.
    /// </summary>
    public void RefreshAllBindings() => this.RaisePropertyChanged(string.Empty);

    /// <summary>
    /// The running machine's input handler, or null when nothing is running.
    /// </summary>
    private Apple2InputHandler? RunningInputHandler
        => _hostApp.CurrentRunningSystem is Apple2System { InputConsumer: Apple2InputHandler handler }
            ? handler
            : null;

    /// <summary>
    /// Whether host keys drive the game port — the same setting the configuration dialog shows, so
    /// the two always agree, and usable whether or not a machine is running.
    ///
    /// Changing it here is not written to disk. It updates the in-memory config, so it applies to
    /// the next start, and is pushed straight into a running machine so it takes effect at once;
    /// saving stays the dialog's job, reached by pressing OK there.
    /// </summary>
    public bool KeyboardJoystickEnabled
    {
        get => _hostApp.CurrentHostSystemConfig is Apple2HostConfig config
               && config.SystemConfig.KeyboardJoystickEnabled;
        set
        {
            if (_hostApp.CurrentHostSystemConfig is not Apple2HostConfig config
                || config.SystemConfig.KeyboardJoystickEnabled == value)
                return;

            config.SystemConfig.KeyboardJoystickEnabled = value;

            // Apply to the machine already running, which took its copy when it started.
            if (RunningInputHandler is { } handler)
                handler.InputConfig.KeyboardJoystickEnabled = value;

            this.RaisePropertyChanged();
        }
    }

    /// <summary>Load/save needs a started (running or paused) system to load into.</summary>
    public bool IsFileOperationEnabled => _hostApp.EmulatorState != EmulatorState.Uninitialized;

    /// <summary>Copy/paste needs a running system: paste feeds keys the machine must consume.</summary>
    public bool IsCopyPasteEnabled => _hostApp.EmulatorState == EmulatorState.Running;

    public void RaiseEmulatorStateChanged()
    {
        this.RaisePropertyChanged(nameof(IsApple2ConfigEnabled));
        // Starting a machine applies the persisted setting, so the checkbox must re-read rather
        // than keep whatever it last showed for the previous machine.
        this.RaisePropertyChanged(nameof(KeyboardJoystickEnabled));
        this.RaisePropertyChanged(nameof(IsFileOperationEnabled));
        this.RaisePropertyChanged(nameof(IsCopyPasteEnabled));
        RaiseDriveStateChanged();   // stopping the emulator empties the drive
    }

    // --- Example files (embedded resources) ---

    public ObservableCollection<KeyValuePair<string, string>> AssemblyExamples { get; } = new();
    public ObservableCollection<KeyValuePair<string, string>> BasicExamples { get; } = new();

    private string _selectedAssemblyExample = "";
    public string SelectedAssemblyExample
    {
        get => _selectedAssemblyExample;
        set => this.RaiseAndSetIfChanged(ref _selectedAssemblyExample, value);
    }

    private string _selectedBasicExample = "";
    public string SelectedBasicExample
    {
        get => _selectedBasicExample;
        set => this.RaiseAndSetIfChanged(ref _selectedBasicExample, value);
    }

    private void InitExampleFiles()
    {
        AssemblyExamples.Clear();
        AssemblyExamples.Add(new KeyValuePair<string, string>("", "-- Select an example --"));
        AssemblyExamples.Add(new KeyValuePair<string, string>(
            $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Apple2.hello_echo.bin", "HelloEcho"));

        BasicExamples.Clear();
        BasicExamples.Add(new KeyValuePair<string, string>("", "-- Select an example --"));
        BasicExamples.Add(new KeyValuePair<string, string>(
            $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Basic.Apple2.HelloWorld.bas", "HelloWorld"));
        BasicExamples.Add(new KeyValuePair<string, string>(
            $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Basic.Apple2.PlayNotes.bas", "PlayNotes"));
    }

    private byte[] ReadExampleResource(string resourceName)
    {
        using var resourceStream = _examplesAssembly.GetManifestResourceStream(resourceName)
            ?? throw new DotNet6502Exception($"Cannot find file in embedded resources. Resource: {resourceName}");
        var bytes = new byte[resourceStream.Length];
        resourceStream.ReadExactly(bytes);
        return bytes;
    }

    private async Task LoadAssemblyExampleAsync()
    {
        if (string.IsNullOrEmpty(SelectedAssemblyExample))
            return;
        var fileBytes = ReadExampleResource(SelectedAssemblyExample);
        await LoadBinaryFileAsync(fileBytes);
    }

    private async Task LoadBasicExampleAsync()
    {
        if (string.IsNullOrEmpty(SelectedBasicExample))
            return;
        var fileBytes = ReadExampleResource(SelectedBasicExample);
        await LoadBasicFileAsync(fileBytes);
    }

    // --- Load/save implementations ---

    /// <summary>
    /// Loads bare tokenized Applesoft bytes at $0801 and initialises the Applesoft zero-page
    /// pointers. Type LIST or RUN at the prompt afterwards.
    /// </summary>
    private async Task LoadBasicFileAsync(byte[] fileBytes)
    {
        var hostApp = HostApp;
        if (hostApp == null || hostApp.EmulatorState == EmulatorState.Uninitialized)
            return;

        bool wasRunning = hostApp.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();

        try
        {
            BinaryLoader.Load(
                hostApp.CurrentRunningSystem!.Mem,
                fileBytes,
                out ushort loadedAtAddress,
                out ushort fileLength,
                forceLoadAddress: Apple2System.BASIC_LOAD_ADDRESS);

            var apple2 = (Apple2System)hostApp.CurrentRunningSystem!;
            apple2.InitBasicMemoryVariables(loadedAtAddress, fileLength);

            _logger.LogInformation($"Basic program loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
        }
        finally
        {
            if (wasRunning)
                await hostApp.Start();
        }
    }

    /// <summary>
    /// Loads a DOS 3.3 "B" file (4-byte header: load address + length) and starts it by
    /// setting the CPU program counter to the load address, the way BRUN does.
    /// </summary>
    private async Task LoadBinaryFileAsync(byte[] fileBytes)
    {
        var hostApp = HostApp;
        if (hostApp == null || hostApp.EmulatorState == EmulatorState.Uninitialized)
            return;

        if (!TryParseDos33BinaryFile(fileBytes, out ushort loadAddress, out var payload))
        {
            _logger.LogError("Not a DOS 3.3 binary (B) file: expected a 4-byte header (load address + length) followed by data.");
            return;
        }

        bool wasRunning = hostApp.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();

        try
        {
            BinaryLoader.Load(
                hostApp.CurrentRunningSystem!.Mem,
                payload,
                out ushort loadedAtAddress,
                out ushort fileLength,
                forceLoadAddress: loadAddress);

            // Start the loaded program (BRUN semantics: entry point == load address).
            hostApp.CurrentRunningSystem.CPU.PC = loadedAtAddress;

            _logger.LogInformation($"Binary loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
            _logger.LogInformation($"Program Counter set to {loadedAtAddress.ToHex()}");
        }
        finally
        {
            if (wasRunning)
                await hostApp.Start();
        }
    }

    /// <summary>
    /// Parses the DOS 3.3 "B" file layout: load address (2 bytes) + length (2 bytes), little
    /// endian, followed by the data. Tolerates trailing padding (e.g. from sector-aligned
    /// extractions) by trusting the header's length.
    /// </summary>
    internal static bool TryParseDos33BinaryFile(byte[] fileBytes, out ushort loadAddress, out byte[] payload)
    {
        loadAddress = 0;
        payload = Array.Empty<byte>();

        if (fileBytes.Length < 5)
            return false;

        loadAddress = (ushort)(fileBytes[0] | (fileBytes[1] << 8));
        int length = fileBytes[2] | (fileBytes[3] << 8);
        if (length == 0 || length > fileBytes.Length - 4)
            return false;

        payload = new byte[length];
        Array.Copy(fileBytes, 4, payload, 0, length);
        return true;
    }

    /// <summary>
    /// Returns the current BASIC program as bare tokenized Applesoft bytes (no header).
    /// </summary>
    public async Task<byte[]> GetBasicProgramAsFileBytesAsync()
    {
        var hostApp = HostApp;
        if (hostApp == null || hostApp.EmulatorState == EmulatorState.Uninitialized)
            return Array.Empty<byte>();

        bool wasRunning = hostApp.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();

        try
        {
            if (hostApp.CurrentRunningSystem is not Apple2System apple2)
                return Array.Empty<byte>();

            var saveData = BinarySaver.BuildSaveData(
                apple2.Mem,
                Apple2System.BASIC_LOAD_ADDRESS,
                apple2.GetBasicProgramEndAddress(),
                addFileHeaderWithLoadAddress: false);
            return saveData;
        }
        finally
        {
            if (wasRunning)
                await hostApp.Start();
        }
    }

    // --- .dsk disk images as a file source: catalog-level access with no drive involved,
    // for RAM-resident programs. Inserting a disk in the emulated Disk II drive, and booting
    // from it, is the separate Disk drive section. ---

    /// <summary>Runnable files (Binary/Applesoft) of the opened disk image, name → display text.</summary>
    public ObservableCollection<KeyValuePair<string, string>> DskFiles { get; } = new();

    private string _selectedDskFile = "";
    public string SelectedDskFile
    {
        get => _selectedDskFile;
        set => this.RaiseAndSetIfChanged(ref _selectedDskFile, value);
    }

    public bool HasAttachedDiskImage => _attachedDiskImage != null;

    private string _diskStatusText = "";
    public string DiskStatusText
    {
        get => _diskStatusText;
        set => this.RaiseAndSetIfChanged(ref _diskStatusText, value);
    }

    /// <summary>Parses a .dsk image and populates the runnable-file list.</summary>
    private void AttachDskImage(byte[] fileBytes)
    {
        try
        {
            var diskImage = DskParser.ParseDskFile(fileBytes, _logger);
            _attachedDiskImage = diskImage;

            DskFiles.Clear();
            foreach (var file in diskImage.Files.Where(f =>
                f.FileType is DskFileType.Binary or DskFileType.ApplesoftBasic))
            {
                var typeLetter = file.FileType == DskFileType.Binary ? "B" : "A";
                DskFiles.Add(new KeyValuePair<string, string>(
                    file.FileName, $"{file.FileName} ({typeLetter}, {file.Sectors} sectors)"));
            }

            SelectedDskFile = diskImage.GetFirstRunnableFileName() ?? "";
            DiskStatusText = DskFiles.Count > 0
                ? $"Disk volume {diskImage.Volume}: {DskFiles.Count} runnable file(s) of {diskImage.Files.Count} total."
                : $"Disk volume {diskImage.Volume}: no runnable (Binary/Applesoft) files found.";
            this.RaisePropertyChanged(nameof(HasAttachedDiskImage));
        }
        catch (Exception ex)
        {
            _attachedDiskImage = null;
            DskFiles.Clear();
            SelectedDskFile = "";
            DiskStatusText = string.IsNullOrWhiteSpace(ex.Message) ? "Could not parse the disk image." : ex.Message;
            this.RaisePropertyChanged(nameof(HasAttachedDiskImage));
            _logger.LogError(ex, "Error parsing .dsk image");
        }
    }

    /// <summary>Loads and runs the selected catalog file from the opened disk image.</summary>
    private async Task RunDskFileAsync()
    {
        if (_attachedDiskImage == null || string.IsNullOrEmpty(SelectedDskFile) ||
            _hostApp.EmulatorState == EmulatorState.Uninitialized)
            return;

        try
        {
            await Apple2AutoLoadAndRun.LoadAndRunFileAsync(_hostApp, _attachedDiskImage, SelectedDskFile, _logger);
        }
        catch (Exception ex)
        {
            DiskStatusText = string.IsNullOrWhiteSpace(ex.Message) ? "Could not load the file." : ex.Message;
            _logger.LogError(ex, "Error loading file from .dsk image");
        }
    }

    // --- Disk drive (Disk II emulation): insert a disk image and boot from it ---

    /// <summary>
    /// Read from the drive itself rather than tracked here, so the button cannot disagree with
    /// the machine — the disk can also be changed from the remote control interface, or vanish
    /// when the emulator is stopped.
    /// </summary>
    public bool HasInsertedDisk
        => _hostApp.CurrentRunningSystem is Apple2System apple2 && apple2.DiskController.IsDiskInserted;

    private void RaiseDriveStateChanged()
    {
        this.RaisePropertyChanged(nameof(HasInsertedDisk));
        this.RaisePropertyChanged(nameof(DiskToggleButtonText));
        this.RaisePropertyChanged(nameof(DriveStatusText));
    }

    /// <summary>
    /// One button covers both directions, like the C64 menu's attach/detach: with a disk in the
    /// drive it ejects, otherwise it asks for one to insert.
    /// </summary>
    public string DiskToggleButtonText => HasInsertedDisk ? "Eject disk" : "Insert .dsk image";

    /// <summary>Last error from a drive operation; cleared as soon as one succeeds.</summary>
    private string? _driveErrorMessage;

    /// <summary>
    /// Derived from the drive for the same reason as <see cref="HasInsertedDisk"/>: a status line
    /// with its own copy of the state goes stale the moment the disk changes by any other route
    /// (the remote control interface, or stopping the emulator).
    /// </summary>
    /// Kept to one short line: the sidebar has no scroll region, so taller section content grows
    /// the window itself. The section's description above already explains the workflow.
    public string DriveStatusText => _driveErrorMessage ?? (HasInsertedDisk
        ? "Disk in drive."
        : "No disk in drive.");

    /// <summary>
    /// Puts a diskette in drive 1 without disturbing the running machine. Resident DOS picks it
    /// up on its next access, so this is the "swap disks" half of the workflow; booting is the
    /// separate <see cref="BootDiskCommand"/>.
    /// </summary>
    private async Task InsertDiskAsync(byte[] fileBytes)
    {
        try
        {
            await Apple2DiskBoot.InsertAsync(_hostApp, fileBytes, _logger);
            _driveErrorMessage = null;
        }
        catch (Exception ex)
        {
            _driveErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? "Could not read the disk image." : ex.Message;
            _logger.LogError(ex, "Error inserting .dsk image");
        }
        RaiseDriveStateChanged();
    }

    /// <summary>Boots the machine from the disk in drive 1 — the equivalent of typing PR#6.</summary>
    private async Task BootDiskAsync()
    {
        try
        {
            await Apple2DiskBoot.BootAsync(_hostApp, _logger);
            _driveErrorMessage = null;
        }
        catch (Exception ex)
        {
            _driveErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? "Could not boot from the disk." : ex.Message;
            _logger.LogError(ex, "Error booting from disk");
        }
        RaiseDriveStateChanged();
    }

    private void EjectDisk()
    {
        Apple2DiskBoot.Eject(_hostApp, _logger);
        _driveErrorMessage = null;
        RaiseDriveStateChanged();
    }

    // --- Download & Run programs ---

    public ObservableCollection<KeyValuePair<string, string>> PreloadedPrograms { get; } = new();

    private string _selectedPreloadedProgram = "";
    public string SelectedPreloadedProgram
    {
        get => _selectedPreloadedProgram;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPreloadedProgram, value);
            LatestPreloadedProgramError = "";
        }
    }

    private bool _isLoadingPreloadedProgram;
    public bool IsLoadingPreloadedProgram
    {
        get => _isLoadingPreloadedProgram;
        set => this.RaiseAndSetIfChanged(ref _isLoadingPreloadedProgram, value);
    }

    private string _latestPreloadedProgramError = "";
    public string LatestPreloadedProgramError
    {
        get => _latestPreloadedProgramError;
        set
        {
            this.RaiseAndSetIfChanged(ref _latestPreloadedProgramError, value);
            this.RaisePropertyChanged(nameof(HasLatestPreloadedProgramError));
        }
    }

    public bool HasLatestPreloadedProgramError => !string.IsNullOrEmpty(LatestPreloadedProgramError);

    private void InitPreloadedPrograms()
    {
        PreloadedPrograms.Clear();
        PreloadedPrograms.Add(new KeyValuePair<string, string>("", "-- Select a program --"));
        foreach (var (key, info) in _preloadedPrograms)
            PreloadedPrograms.Add(new KeyValuePair<string, string>(key, info.DisplayName));
    }

    private async Task LoadPreloadedProgramAsync()
    {
        if (string.IsNullOrEmpty(SelectedPreloadedProgram) ||
            !_preloadedPrograms.TryGetValue(SelectedPreloadedProgram, out var programInfo))
            return;

        IsLoadingPreloadedProgram = true;
        LatestPreloadedProgramError = "";
        try
        {
            _apple2AutoLoadAndRun ??= new Apple2AutoLoadAndRun(
                _loggerFactory,
                _httpClient,
                _hostApp,
                corsProxyUrl: _hostApp.GetCorsProxyUrl(),
                downloadCache: _hostApp.GetDownloadCache());

            await _apple2AutoLoadAndRun.DownloadAndRunProgram(
                programInfo,
                setConfigCallback: info =>
                {
                    // Applied while the emulator is stopped so the machine starts with it. Only the
                    // in-memory config is touched — as with the sidebar checkbox, launching a game
                    // must not quietly rewrite the user's saved settings.
                    if (_hostApp.CurrentHostSystemConfig is Apple2HostConfig apple2HostConfig)
                        apple2HostConfig.SystemConfig.KeyboardJoystickEnabled = info.KeyboardJoystickEnabled;
                    return Task.CompletedTask;
                });

            this.RaisePropertyChanged(nameof(KeyboardJoystickEnabled));
        }
        catch (Exception ex)
        {
            LatestPreloadedProgramError = string.IsNullOrWhiteSpace(ex.Message)
                ? "Could not download and start the program."
                : ex.Message;
            _logger.LogError(ex, "Error downloading and running program {Program}", programInfo.DisplayName);
        }
        finally
        {
            IsLoadingPreloadedProgram = false;
        }
    }

    // --- Copy/paste of Applesoft BASIC ---

    /// <summary>
    /// Detokenizes the Applesoft program in memory and asks the View to put the source text on
    /// the clipboard.
    /// </summary>
    private async Task CopyBasicSourceCodeAsync()
    {
        if (_hostApp.EmulatorState != EmulatorState.Running ||
            _hostApp.CurrentRunningSystem is not Apple2System apple2)
            return;

        try
        {
            var sourceCode = apple2.BasicTokenParser.GetBasicText();
            await RequestClipboardCopyAsync(sourceCode);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error copying Basic source: {ex.Message}");
        }
    }

    /// <summary>
    /// Asks the View for the clipboard text and types it into the machine via the keyboard
    /// latch (letters become uppercase; the II Plus has no lowercase).
    /// </summary>
    private async Task PasteTextInternalAsync()
    {
        if (_hostApp.EmulatorState != EmulatorState.Running ||
            _hostApp.CurrentRunningSystem is not Apple2System apple2)
            return;

        try
        {
            var text = await RequestClipboardPasteAsync();
            if (!string.IsNullOrEmpty(text))
                apple2.TextPaste.Paste(text);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error pasting text: {ex.Message}");
        }
    }

    // Events for View to handle clipboard operations
    public event EventHandler<string>? ClipboardCopyRequested;
    public event EventHandler<TaskCompletionSource<string?>>? ClipboardPasteRequested;

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

    // --- ISystemMenuContributor ---

    public string MenuLabel => "Apple II";

    public IReadOnlyList<NativeMenuItemBase> GetNativeMenuItems()
    {
        const KeyModifiers macBase = KeyModifiers.Meta | KeyModifiers.Alt;
        const KeyModifiers macShift = KeyModifiers.Meta | KeyModifiers.Alt | KeyModifiers.Shift;

        return new NativeMenuItemBase[]
        {
            BuildMenuItem("Toggle Download & Run section", new KeyGesture(Key.D, macShift), ToggleDownloadSectionCommand),
            BuildMenuItem("Toggle Load/Save section", new KeyGesture(Key.L, macShift), ToggleLoadSaveSectionCommand),
            BuildMenuItem("Toggle Configuration section", new KeyGesture(Key.C, macShift), ToggleConfigSectionCommand),
            new NativeMenuItemSeparator(),
            // Same key as the C64's and VIC-20's, so the shortcut means the same thing whichever
            // machine is loaded.
            BuildMenuItem("Toggle Joystick KB", new KeyGesture(Key.K, macBase), ToggleJoystickKeyboardCommand),
        };
    }

    public IReadOnlyList<KeyBinding> GetKeyBindings()
    {
        const KeyModifiers nonMacBase = KeyModifiers.Control | KeyModifiers.Alt;
        const KeyModifiers nonMacShift = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;

        return new[]
        {
            BuildKeyBinding(new KeyGesture(Key.D, nonMacShift), ToggleDownloadSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.L, nonMacShift), ToggleLoadSaveSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.C, nonMacShift), ToggleConfigSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.K, nonMacBase), ToggleJoystickKeyboardCommand),
        };
    }

    private static NativeMenuItem BuildMenuItem(string header, KeyGesture gesture, ICommand command)
        => new(header)
        {
            Gesture = gesture,
            Command = command,
        };

    private static KeyBinding BuildKeyBinding(KeyGesture gesture, ICommand command)
        => new()
        {
            Gesture = gesture,
            Command = command,
        };
}
