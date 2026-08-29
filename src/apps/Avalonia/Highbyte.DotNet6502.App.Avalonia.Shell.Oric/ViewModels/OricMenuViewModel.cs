using System.Reactive;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Oric.ViewModels;

public sealed class OricMenuViewModel : ViewModelBase
{
    private readonly ILogger _logger;

    public OricMenuViewModel(AvaloniaHostApp hostApp, ILoggerFactory loggerFactory)
    {
        HostApp = hostApp;
        _logger = loggerFactory.CreateLogger(nameof(OricMenuViewModel));
        hostApp.WhenAnyValue(app => app.EmulatorState)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsConfigEnabled));
                this.RaisePropertyChanged(nameof(IsCopyPasteEnabled));
            });

        CopyBasicSourceCommand = ReactiveCommandHelper.CreateSafeCommand(
            CopyBasicSourceCodeAsync,
            this.WhenAnyValue(viewModel => viewModel.IsCopyPasteEnabled),
            RxSchedulers.MainThreadScheduler);

        PasteTextCommand = ReactiveCommandHelper.CreateSafeCommand(
            PasteTextInternalAsync,
            this.WhenAnyValue(viewModel => viewModel.IsCopyPasteEnabled),
            RxSchedulers.MainThreadScheduler);
    }

    public AvaloniaHostApp HostApp { get; }
    public bool IsConfigEnabled => HostApp.EmulatorState == EmulatorState.Uninitialized;
    public bool IsCopyPasteEnabled => HostApp.EmulatorState == EmulatorState.Running;

    public ReactiveCommand<Unit, Unit> CopyBasicSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteTextCommand { get; }

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
}
