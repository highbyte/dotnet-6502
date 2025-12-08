using System;
using System.Collections.ObjectModel;
using System.Reactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class DebugSoundViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;

    private SoundTest _selectedSoundTest;

    private bool _isBusy;
    private string? _statusMessage;
    private readonly ObservableCollection<string> _logMessages = new();

    // ReactiveUI Commands
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public enum SoundTest
    {
        TestTone,
        NoiseGeneration,
        WaveformSynthesis
    }

    public DebugSoundViewModel(
        AvaloniaHostApp hostApp,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _configuration = configuration;
        _loggerFactory = loggerFactory;

        CloseCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                CloseRequested?.Invoke(this, true);
            },
            outputScheduler: RxApp.MainThreadScheduler);
    }

    public event EventHandler<bool>? CloseRequested;

    public bool IsRunningInWebAssembly { get; } = PlatformDetection.IsRunningInWebAssembly();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            this.RaiseAndSetIfChanged(ref _isBusy, value);
            this.RaisePropertyChanged(nameof(IsNotBusy));
            this.RaisePropertyChanged(nameof(CanClose));
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool CanClose => IsNotBusy;

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
                return;

            this.RaiseAndSetIfChanged(ref _statusMessage, value);
            this.RaisePropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public ObservableCollection<string> LogMessages => _logMessages;

    public bool HasLogMessages => _logMessages.Count > 0;

    public SoundTest SelectedSoundTest
    {
        get => _selectedSoundTest;
        set
        {
            _selectedSoundTest = value;
            this.RaisePropertyChanged();
        }
    }

    public SoundTest[] SoundTestValues { get; } = Enum.GetValues<SoundTest>();
}
