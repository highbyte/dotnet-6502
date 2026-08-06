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

    private enum Apple2MenuSection { Download, LoadSave, Config }

    // Accordion behavior (like the C64 menu): expanding one section collapses the others.
    private readonly AccordionSections<Apple2MenuSection> _sections;

    private readonly HttpClient _httpClient = new();
    private DskDiskImage? _attachedDiskImage;
    private Apple2AutoLoadAndRun? _apple2AutoLoadAndRun;

    /// <summary>
    /// The curated "Download & Run" list. Only RAM-resident programs belong here — the machine
    /// has no Disk II emulation, so anything that touches the disk at runtime will not work.
    /// Each entry must be verified in the running emulator before inclusion.
    /// </summary>
    private readonly Dictionary<string, Apple2DownloadProgramInfo> _preloadedPrograms = new()
    {
        { "applepanic", new Apple2DownloadProgramInfo(
            "Apple Panic",
            "https://mirrors.apple2.org.za/ftp.apple.asimov.net/images/games/action/apple_panic/apple_panic.dsk") },
    };

    public AvaloniaHostApp HostApp => _hostApp;

    public ReactiveCommand<Unit, Unit> ToggleConfigSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleLoadSaveSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleDownloadSectionCommand { get; }
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

        ToggleConfigSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                _sections.Toggle(Apple2MenuSection.Config);
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

        InitExampleFiles();
        InitPreloadedPrograms();
    }

    public bool IsConfigSectionExpanded => _sections.IsExpanded(Apple2MenuSection.Config);

    public bool IsLoadSaveSectionExpanded => _sections.IsExpanded(Apple2MenuSection.LoadSave);

    public bool IsDownloadSectionExpanded => _sections.IsExpanded(Apple2MenuSection.Download);

    private void OnSectionStateChanged(Apple2MenuSection section)
    {
        var propertyName = section switch
        {
            Apple2MenuSection.Download => nameof(IsDownloadSectionExpanded),
            Apple2MenuSection.LoadSave => nameof(IsLoadSaveSectionExpanded),
            _ => nameof(IsConfigSectionExpanded),
        };
        this.RaisePropertyChanged(propertyName);
    }

    /// <summary>Configuration may only be edited while the emulator is not running.</summary>
    public bool IsApple2ConfigEnabled => _hostApp.EmulatorState == EmulatorState.Uninitialized;

    /// <summary>Load/save needs a started (running or paused) system to load into.</summary>
    public bool IsFileOperationEnabled => _hostApp.EmulatorState != EmulatorState.Uninitialized;

    /// <summary>Copy/paste needs a running system: paste feeds keys the machine must consume.</summary>
    public bool IsCopyPasteEnabled => _hostApp.EmulatorState == EmulatorState.Running;

    public void RaiseEmulatorStateChanged()
    {
        this.RaisePropertyChanged(nameof(IsApple2ConfigEnabled));
        this.RaisePropertyChanged(nameof(IsFileOperationEnabled));
        this.RaisePropertyChanged(nameof(IsCopyPasteEnabled));
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

    // --- .dsk disk images as a file source (file-level access — no Disk II hardware
    // emulation, so this is a program-loading feature, not a drive "attach". The
    // attach-disk-image concept is reserved for future Disk II emulation.) ---

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

            await _apple2AutoLoadAndRun.DownloadAndRunProgram(programInfo);
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
        const KeyModifiers macShift = KeyModifiers.Meta | KeyModifiers.Alt | KeyModifiers.Shift;

        return new NativeMenuItemBase[]
        {
            BuildMenuItem("Toggle Download & Run section", new KeyGesture(Key.D, macShift), ToggleDownloadSectionCommand),
            BuildMenuItem("Toggle Load/Save section", new KeyGesture(Key.L, macShift), ToggleLoadSaveSectionCommand),
            BuildMenuItem("Toggle Configuration section", new KeyGesture(Key.C, macShift), ToggleConfigSectionCommand),
        };
    }

    public IReadOnlyList<KeyBinding> GetKeyBindings()
    {
        const KeyModifiers nonMacShift = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;

        return new[]
        {
            BuildKeyBinding(new KeyGesture(Key.D, nonMacShift), ToggleDownloadSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.L, nonMacShift), ToggleLoadSaveSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.C, nonMacShift), ToggleConfigSectionCommand),
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
