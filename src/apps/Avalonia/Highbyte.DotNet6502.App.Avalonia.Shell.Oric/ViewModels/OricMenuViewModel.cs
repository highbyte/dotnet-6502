using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia.Oric;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using Highbyte.DotNet6502.Systems.Oric.Tape.Download;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Oric.ViewModels;

public sealed class OricMenuViewModel : ViewModelBase, ISystemMenuContributor
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Assembly _examplesAssembly = typeof(OricMenuViewModel).Assembly;
    private readonly HttpClient _httpClient = new();
    private OricAutoLoadAndRun? _oricAutoLoadAndRun;
    private readonly AccordionSections<OricMenuSection> _sections;

    private readonly Dictionary<string, OricDownloadProgramInfo> _preloadedPrograms = new()
    {
        { "xenon1", new OricDownloadProgramInfo(
            "Xenon 1",
            "https://cdn.oric.org/games/software/x/xenon1/Xenon1.tap") },
        { "zorgonsrevenge", new OricDownloadProgramInfo(
            "Zorgon's Revenge",
            "https://cdn.oric.org/games/software/z/zorgon/zorgons.tap") },
        { "manicminer", new OricDownloadProgramInfo(
            "Manic Miner",
            "https://cdn.oric.org/games/software/m/manic_miner/MANICMINER_proper.tap") },
        { "thehobbit", new OricDownloadProgramInfo(
            "The Hobbit",
            "https://cdn.oric.org/games/software/t/tansoft_editor/hobbit.tap") },
        { "stormlord", new OricDownloadProgramInfo(
            "Stormlord",
            "https://cdn.oric.org/games/software/s/stormlord/Storm.tap",
            joystickInterface: OricJoystickInterface.IJK,
            keyboardJoystickEnabled: false,
            keyboardJoystickNumber: 1) }, // Stormlord supports PASE and IJK and reads both ports; prefer IJK to preserve sound.
    };

    public OricMenuViewModel(AvaloniaHostApp hostApp, ILoggerFactory loggerFactory)
    {
        HostApp = hostApp;
        _logger = loggerFactory.CreateLogger(nameof(OricMenuViewModel));
        _loggerFactory = loggerFactory;
        _sections = new AccordionSections<OricMenuSection>(
            OnSectionStateChanged, initiallyExpanded: OricMenuSection.Download);
        InitializeExamples();
        InitializePreloadedPrograms();
        InitializeJoystickOptions();
        hostApp.WhenAnyValue(app => app.EmulatorState)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsConfigEnabled));
                this.RaisePropertyChanged(nameof(IsCopyPasteEnabled));
                this.RaisePropertyChanged(nameof(IsFileOperationEnabled));
                RefreshJoystickProperties();
            });

        CopyBasicSourceCommand = ReactiveCommandHelper.CreateSafeCommand(
            CopyBasicSourceCodeAsync,
            this.WhenAnyValue(viewModel => viewModel.IsCopyPasteEnabled),
            RxSchedulers.MainThreadScheduler);

        PasteTextCommand = ReactiveCommandHelper.CreateSafeCommand(
            PasteTextInternalAsync,
            this.WhenAnyValue(viewModel => viewModel.IsCopyPasteEnabled),
            RxSchedulers.MainThreadScheduler);

        LoadBasicTapFileCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            tapBytes => LoadBasicTapAsync(tapBytes),
            this.WhenAnyValue(viewModel => viewModel.IsFileOperationEnabled),
            RxSchedulers.MainThreadScheduler);

        LoadBasicExampleCommand = ReactiveCommandHelper.CreateSafeCommand(
            LoadBasicExampleAsync,
            this.WhenAnyValue(viewModel => viewModel.IsFileOperationEnabled),
            RxSchedulers.MainThreadScheduler);

        LoadPreloadedProgramCommand = ReactiveCommandHelper.CreateSafeCommand(
            LoadPreloadedProgramAsync,
            this.WhenAnyValue(viewModel => viewModel.IsLoadingPreloadedProgram)
                .Select(isLoading => !isLoading),
            RxSchedulers.MainThreadScheduler);

        ToggleDownloadSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => _sections.Toggle(OricMenuSection.Download),
            null,
            RxSchedulers.MainThreadScheduler);

        ToggleLoadSaveSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => _sections.Toggle(OricMenuSection.LoadSave),
            null,
            RxSchedulers.MainThreadScheduler);

        ToggleConfigSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => _sections.Toggle(OricMenuSection.Config),
            null,
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

    public AvaloniaHostApp HostApp { get; }
    public bool IsConfigEnabled => HostApp.EmulatorState == EmulatorState.Uninitialized;
    public bool IsCopyPasteEnabled => HostApp.EmulatorState == EmulatorState.Running;
    public bool IsFileOperationEnabled => HostApp.EmulatorState != EmulatorState.Uninitialized;

    public ReactiveCommand<Unit, Unit> CopyBasicSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteTextCommand { get; }
    public ReactiveCommand<byte[], Unit> LoadBasicTapFileCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadBasicExampleCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadPreloadedProgramCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleDownloadSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleLoadSaveSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleConfigSectionCommand { get; }
    public ReactiveCommand<int, Unit> SetActiveJoystickCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleJoystickKeyboardCommand { get; }
    public ReactiveCommand<int, Unit> SetKeyboardJoystickCommand { get; }

    public ObservableCollection<KeyValuePair<string, string>> BasicExamples { get; } = new();
    public ObservableCollection<KeyValuePair<string, string>> PreloadedPrograms { get; } = new();
    public ObservableCollection<KeyValuePair<OricJoystickInterface, string>> JoystickInterfaces { get; } = new();
    public ObservableCollection<int> AvailableJoysticks { get; } = new();

    private OricHostConfig? OricHostConfig => HostApp.CurrentHostSystemConfig as OricHostConfig;

    public bool HasConfigValidationErrors
        => OricHostConfig != null && !OricHostConfig.IsValid(out _);

    public bool IsDownloadSectionExpanded => _sections.IsExpanded(OricMenuSection.Download);
    public bool IsLoadSaveSectionExpanded => _sections.IsExpanded(OricMenuSection.LoadSave);
    public bool IsConfigSectionExpanded => _sections.IsExpanded(OricMenuSection.Config);

    public void ExpandConfigSectionOnValidationError()
        => _sections.SetExpanded(OricMenuSection.Config, true);

    public OricJoystickInterface JoystickInterface
    {
        get => OricHostConfig?.SystemConfig.JoystickInterface ?? OricJoystickInterface.None;
        set
        {
            if (OricHostConfig == null || OricHostConfig.SystemConfig.JoystickInterface == value)
                return;
            OricHostConfig.SystemConfig.JoystickInterface = value;
            if (HostApp.CurrentRunningSystem is OricMachine oric)
                oric.Joystick.Interface = value;
            else
                HostApp.UpdateHostSystemConfig(OricHostConfig);
            this.RaisePropertyChanged();
        }
    }

    public int CurrentJoystick
    {
        get => OricHostConfig?.InputConfig.CurrentJoystick ?? 1;
        set
        {
            if (OricHostConfig == null || OricHostConfig.InputConfig.CurrentJoystick == value)
                return;
            OricHostConfig.InputConfig.CurrentJoystick = value;
            if (HostApp.EmulatorState == EmulatorState.Uninitialized)
                HostApp.UpdateHostSystemConfig(OricHostConfig);
            this.RaisePropertyChanged();
        }
    }

    public bool JoystickKeyboardEnabled
    {
        get => OricHostConfig?.SystemConfig.KeyboardJoystickEnabled ?? false;
        set
        {
            if (OricHostConfig == null || OricHostConfig.SystemConfig.KeyboardJoystickEnabled == value)
                return;
            OricHostConfig.SystemConfig.KeyboardJoystickEnabled = value;
            if (HostApp.CurrentRunningSystem is OricMachine oric)
                oric.Joystick.KeyboardJoystickEnabled = value;
            else
                HostApp.UpdateHostSystemConfig(OricHostConfig);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsKeyboardJoystickSelectionEnabled));
        }
    }

    public int KeyboardJoystick
    {
        get => OricHostConfig?.SystemConfig.KeyboardJoystick ?? 1;
        set
        {
            if (OricHostConfig == null || OricHostConfig.SystemConfig.KeyboardJoystick == value)
                return;
            OricHostConfig.SystemConfig.KeyboardJoystick = value;
            if (HostApp.CurrentRunningSystem is OricMachine oric)
                oric.Joystick.KeyboardJoystick = value;
            else
                HostApp.UpdateHostSystemConfig(OricHostConfig);
            this.RaisePropertyChanged();
        }
    }

    public bool IsKeyboardJoystickSelectionEnabled => JoystickKeyboardEnabled;

    private string _selectedBasicExample = string.Empty;
    public string SelectedBasicExample
    {
        get => _selectedBasicExample;
        set => this.RaiseAndSetIfChanged(ref _selectedBasicExample, value);
    }

    private string _selectedPreloadedProgram = string.Empty;
    public string SelectedPreloadedProgram
    {
        get => _selectedPreloadedProgram;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPreloadedProgram, value);
            this.RaisePropertyChanged(nameof(HasSelectedPreloadedProgram));
            LatestPreloadedProgramError = string.Empty;
        }
    }

    public bool HasSelectedPreloadedProgram => !string.IsNullOrEmpty(SelectedPreloadedProgram);

    private bool _isLoadingPreloadedProgram;
    public bool IsLoadingPreloadedProgram
    {
        get => _isLoadingPreloadedProgram;
        private set => this.RaiseAndSetIfChanged(ref _isLoadingPreloadedProgram, value);
    }

    private string _latestPreloadedProgramError = string.Empty;
    public string LatestPreloadedProgramError
    {
        get => _latestPreloadedProgramError;
        private set
        {
            this.RaiseAndSetIfChanged(ref _latestPreloadedProgramError, value);
            this.RaisePropertyChanged(nameof(HasLatestPreloadedProgramError));
        }
    }

    public bool HasLatestPreloadedProgramError => !string.IsNullOrEmpty(LatestPreloadedProgramError);

    private enum OricMenuSection { Download, LoadSave, Config }

    private void OnSectionStateChanged(OricMenuSection section)
    {
        switch (section)
        {
            case OricMenuSection.Download:
                this.RaisePropertyChanged(nameof(IsDownloadSectionExpanded));
                break;
            case OricMenuSection.LoadSave:
                this.RaisePropertyChanged(nameof(IsLoadSaveSectionExpanded));
                break;
            case OricMenuSection.Config:
                this.RaisePropertyChanged(nameof(IsConfigSectionExpanded));
                break;
        }
    }

    public event EventHandler<string>? ClipboardCopyRequested;
    public event EventHandler<TaskCompletionSource<string?>>? ClipboardPasteRequested;

    private async Task CopyBasicSourceCodeAsync()
    {
        if (HostApp.EmulatorState != EmulatorState.Running ||
            HostApp.CurrentRunningSystem is not OricMachine oric)
        {
            return;
        }

        try
        {
            var sourceCode = oric.BasicTokenParser.GetBasicText();
            ClipboardCopyRequested?.Invoke(this, sourceCode);
            await Task.CompletedTask;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error copying Oric BASIC source");
        }
    }

    private async Task PasteTextInternalAsync()
    {
        if (HostApp.EmulatorState != EmulatorState.Running ||
            HostApp.CurrentRunningSystem is not OricMachine oric ||
            ClipboardPasteRequested == null)
        {
            return;
        }

        try
        {
            var completion = new TaskCompletionSource<string?>();
            ClipboardPasteRequested.Invoke(this, completion);
            var text = await completion.Task;
            if (!string.IsNullOrEmpty(text))
                oric.TextPaste.Paste(text);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error pasting text into Oric");
        }
    }

    private void InitializeExamples()
    {
        var assemblyName = _examplesAssembly.GetName().Name;
        BasicExamples.Add(new KeyValuePair<string, string>(string.Empty, "-- Select an example --"));
        AddBasicExample(assemblyName, "HelloWorld", "Hello World");
        AddBasicExample(assemblyName, "HiresShapes", "Hires Shapes");
        AddBasicExample(assemblyName, "Fireworks", "Fireworks");
        AddBasicExample(assemblyName, "SoundEffects", "Sound Effects");
        AddBasicExample(assemblyName, "ThreeVoiceMusic", "Three-Voice Music");
        AddBasicExample(assemblyName, "AySoundLab", "AY Sound Lab");
    }

    private void AddBasicExample(string? assemblyName, string fileName, string displayName)
        => BasicExamples.Add(new KeyValuePair<string, string>(
            $"{assemblyName}.Resources.Sample6502Programs.Basic.Oric.{fileName}.tap",
            displayName));

    private void InitializePreloadedPrograms()
    {
        PreloadedPrograms.Add(new KeyValuePair<string, string>(string.Empty, "-- Select a program --"));
        foreach (var (key, programInfo) in _preloadedPrograms)
            PreloadedPrograms.Add(new KeyValuePair<string, string>(key, programInfo.DisplayName));
    }

    private void InitializeJoystickOptions()
    {
        JoystickInterfaces.Add(new(OricJoystickInterface.None, "None"));
        JoystickInterfaces.Add(new(OricJoystickInterface.PASE, "PASE"));
        JoystickInterfaces.Add(new(OricJoystickInterface.IJK, "IJK"));
        foreach (var joystick in OricHostConfig?.InputConfig.AvailableJoysticks ?? [1, 2])
            AvailableJoysticks.Add(joystick);
    }

    public void RefreshJoystickProperties()
    {
        this.RaisePropertyChanged(nameof(JoystickInterface));
        this.RaisePropertyChanged(nameof(CurrentJoystick));
        this.RaisePropertyChanged(nameof(JoystickKeyboardEnabled));
        this.RaisePropertyChanged(nameof(KeyboardJoystick));
        this.RaisePropertyChanged(nameof(IsKeyboardJoystickSelectionEnabled));
    }

    private async Task LoadPreloadedProgramAsync()
    {
        if (string.IsNullOrEmpty(SelectedPreloadedProgram) ||
            !_preloadedPrograms.TryGetValue(SelectedPreloadedProgram, out var programInfo))
        {
            return;
        }

        IsLoadingPreloadedProgram = true;
        LatestPreloadedProgramError = string.Empty;
        try
        {
            _oricAutoLoadAndRun ??= new OricAutoLoadAndRun(
                _loggerFactory,
                _httpClient,
                HostApp,
                corsProxyUrl: HostApp.GetCorsProxyUrl(),
                downloadCache: HostApp.GetDownloadCache());

            await _oricAutoLoadAndRun.DownloadAndRunProgram(
                programInfo,
                setConfigCallback: ConfigureForPreloadedProgramAsync);
        }
        catch (Exception exception)
        {
            LatestPreloadedProgramError = string.IsNullOrWhiteSpace(exception.Message)
                ? "Could not download and start the program."
                : exception.Message;
            _logger.LogError(
                exception,
                "Error downloading and running Oric program {Program}",
                programInfo.DisplayName);
        }
        finally
        {
            IsLoadingPreloadedProgram = false;
        }
    }

    private Task ConfigureForPreloadedProgramAsync(OricDownloadProgramInfo programInfo)
    {
        if (OricHostConfig is not { } oricHostConfig)
            return Task.CompletedTask;

        oricHostConfig.SystemConfig.JoystickInterface = programInfo.JoystickInterface;
        oricHostConfig.SystemConfig.KeyboardJoystickEnabled = programInfo.KeyboardJoystickEnabled;
        oricHostConfig.SystemConfig.KeyboardJoystick = programInfo.KeyboardJoystickNumber;
        oricHostConfig.InputConfig.CurrentJoystick = programInfo.KeyboardJoystickNumber;
        HostApp.UpdateHostSystemConfig(oricHostConfig);
        RefreshJoystickProperties();
        return Task.CompletedTask;
    }

    private async Task LoadBasicExampleAsync()
    {
        if (string.IsNullOrEmpty(SelectedBasicExample))
            return;

        try
        {
            using var resourceStream = _examplesAssembly.GetManifestResourceStream(SelectedBasicExample)
                ?? throw new InvalidOperationException(
                    $"Cannot find embedded Oric BASIC example resource: {SelectedBasicExample}");
            var tapBytes = new byte[resourceStream.Length];
            resourceStream.ReadExactly(tapBytes);
            await LoadBasicTapAsync(tapBytes, commandAfterLoad: "LIST\n");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error loading Oric BASIC example");
        }
    }

    private async Task LoadBasicTapAsync(byte[] tapBytes, string? commandAfterLoad = null)
    {
        if (HostApp.EmulatorState == EmulatorState.Uninitialized ||
            HostApp.CurrentRunningSystem is not OricMachine oric)
        {
            return;
        }

        var wasRunning = HostApp.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            HostApp.Pause();

        try
        {
            OricTapFile tapFile = oric.LoadBasicTap(tapBytes);
            _logger.LogInformation(
                "Loaded Oric BASIC TAP file {Name} at {StartAddress}-{EndAddress}, length {Length}",
                tapFile.Name,
                tapFile.StartAddress.ToHex(),
                tapFile.EndAddress.ToHex(),
                tapFile.Data.Length);

            if (tapFile.IsAutoRun)
                oric.TextPaste.Paste("RUN\n");
            else if (!string.IsNullOrEmpty(commandAfterLoad))
                oric.TextPaste.Paste(commandAfterLoad);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error loading Oric BASIC .tap file");
        }
        finally
        {
            if (wasRunning)
                await HostApp.Start();
        }
    }

    public string MenuLabel => "Oric";

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
        const KeyModifiers nonMacBase = KeyModifiers.Control | KeyModifiers.Alt;
        const KeyModifiers nonMacShift = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;

        return new[]
        {
            BuildKeyBinding(new KeyGesture(Key.D, nonMacShift), ToggleDownloadSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.L, nonMacShift), ToggleLoadSaveSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.C, nonMacShift), ToggleConfigSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.D1, nonMacBase), SetActiveJoystickCommand, 1),
            BuildKeyBinding(new KeyGesture(Key.D2, nonMacBase), SetActiveJoystickCommand, 2),
            BuildKeyBinding(new KeyGesture(Key.K, nonMacBase), ToggleJoystickKeyboardCommand),
            BuildKeyBinding(new KeyGesture(Key.D1, nonMacShift), SetKeyboardJoystickCommand, 1),
            BuildKeyBinding(new KeyGesture(Key.D2, nonMacShift), SetKeyboardJoystickCommand, 2),
        };
    }

    private static NativeMenuItem BuildMenuItem(
        string header,
        KeyGesture gesture,
        System.Windows.Input.ICommand command,
        object? parameter = null)
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

    private static KeyBinding BuildKeyBinding(
        KeyGesture gesture,
        System.Windows.Input.ICommand command,
        object? parameter = null)
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
}
