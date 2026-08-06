using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
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

    private readonly Assembly _examplesAssembly = typeof(AvaloniaHostApp).Assembly;
    private string? ExampleFileAssemblyName => _examplesAssembly.GetName().Name;

    private bool _isConfigSectionExpanded = true;
    private bool _isLoadSaveSectionExpanded = true;

    public AvaloniaHostApp HostApp => _hostApp;

    public ReactiveCommand<Unit, Unit> ToggleConfigSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleLoadSaveSectionCommand { get; }
    public ReactiveCommand<byte[], Unit> LoadBasicFileCommand { get; }
    public ReactiveCommand<byte[], Unit> LoadBinaryFileCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadAssemblyExampleCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadBasicExampleCommand { get; }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ReactiveCommand usage is limited to application-defined view models rooted by the host application.")]
    public Apple2MenuViewModel(AvaloniaHostApp hostApp, ILoggerFactory loggerFactory)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _logger = loggerFactory.CreateLogger(nameof(Apple2MenuViewModel));

        _hostApp
            .WhenAnyValue(x => x.EmulatorState)
            .Subscribe(_ => RaiseEmulatorStateChanged());

        ToggleConfigSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                IsConfigSectionExpanded = !IsConfigSectionExpanded;
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        ToggleLoadSaveSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                IsLoadSaveSectionExpanded = !IsLoadSaveSectionExpanded;
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

        InitExampleFiles();
    }

    public bool IsConfigSectionExpanded
    {
        get => _isConfigSectionExpanded;
        set => this.RaiseAndSetIfChanged(ref _isConfigSectionExpanded, value);
    }

    public bool IsLoadSaveSectionExpanded
    {
        get => _isLoadSaveSectionExpanded;
        set => this.RaiseAndSetIfChanged(ref _isLoadSaveSectionExpanded, value);
    }

    /// <summary>Configuration may only be edited while the emulator is not running.</summary>
    public bool IsApple2ConfigEnabled => _hostApp.EmulatorState == EmulatorState.Uninitialized;

    /// <summary>Load/save needs a started (running or paused) system to load into.</summary>
    public bool IsFileOperationEnabled => _hostApp.EmulatorState != EmulatorState.Uninitialized;

    public void RaiseEmulatorStateChanged()
    {
        this.RaisePropertyChanged(nameof(IsApple2ConfigEnabled));
        this.RaisePropertyChanged(nameof(IsFileOperationEnabled));
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

    // --- ISystemMenuContributor ---

    public string MenuLabel => "Apple II";

    public IReadOnlyList<NativeMenuItemBase> GetNativeMenuItems()
    {
        const KeyModifiers macShift = KeyModifiers.Meta | KeyModifiers.Alt | KeyModifiers.Shift;

        return new NativeMenuItemBase[]
        {
            BuildMenuItem("Toggle Load/Save section", new KeyGesture(Key.L, macShift), ToggleLoadSaveSectionCommand),
            BuildMenuItem("Toggle Configuration section", new KeyGesture(Key.C, macShift), ToggleConfigSectionCommand),
        };
    }

    public IReadOnlyList<KeyBinding> GetKeyBindings()
    {
        const KeyModifiers nonMacShift = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;

        return new[]
        {
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
