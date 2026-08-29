using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using Highbyte.DotNet6502.Systems.Oric.Tape.Download;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Oric.ViewModels;

public sealed class OricMenuViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Assembly _examplesAssembly = typeof(OricMenuViewModel).Assembly;
    private readonly HttpClient _httpClient = new();
    private OricAutoLoadAndRun? _oricAutoLoadAndRun;

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
            "https://cdn.oric.org/games/software/s/stormlord/Storm.tap") },
    };

    public OricMenuViewModel(AvaloniaHostApp hostApp, ILoggerFactory loggerFactory)
    {
        HostApp = hostApp;
        _logger = loggerFactory.CreateLogger(nameof(OricMenuViewModel));
        _loggerFactory = loggerFactory;
        InitializeExamples();
        InitializePreloadedPrograms();
        hostApp.WhenAnyValue(app => app.EmulatorState)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsConfigEnabled));
                this.RaisePropertyChanged(nameof(IsCopyPasteEnabled));
                this.RaisePropertyChanged(nameof(IsFileOperationEnabled));
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

    public ObservableCollection<KeyValuePair<string, string>> BasicExamples { get; } = new();
    public ObservableCollection<KeyValuePair<string, string>> PreloadedPrograms { get; } = new();

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

            await _oricAutoLoadAndRun.DownloadAndRunProgram(programInfo);
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
}
