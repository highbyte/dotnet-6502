using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Impl.NAudio.Synth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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

    public ReactiveCommand<Unit, Unit> InitAudioCommand { get; }
    public ReactiveCommand<Unit, Unit> PlayAudioCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseAudioCommand { get; }
    public ReactiveCommand<Unit, Unit> StopAudioCommand { get; }

    public ReactiveCommand<Unit, Unit> PlayCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }


    public ReactiveCommand<Unit, Unit> PlaySynthCommand { get; }
    public ReactiveCommand<Unit, Unit> StartSynthReleaseCommand { get; }
    public ReactiveCommand<Unit, Unit> StopSynthCommand { get; }

    public enum SoundTest
    {
        TestTone,
        NoiseGeneration,
        WaveformSynthesis
    }

    // ------------------------------------------------------------------
    // NAudio
    // ------------------------------------------------------------------
    private readonly IWavePlayer _wavePlayer; // Should be set differently depending on platform (e.g. WebAudioWavePlayer for WASM, and SilkNetOpenALWavePlayer for desktop)
    private MixingSampleProvider? _mixer;
    private VolumeSampleProvider? _volumeSampleProvider;


    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ReactiveUI WhenAnyValue is used intentionally for ViewModel bindings; members are rooted by XAML and direct references.")]
    public DebugSoundViewModel(
        AvaloniaHostApp hostApp,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IWavePlayer wavePlayer)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _wavePlayer = wavePlayer;

        CloseCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                CloseRequested?.Invoke(this, true);
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        InitAudioCommand = ReactiveCommandHelper.CreateSafeCommand(
            InitAudioAsync,
            canExecute: this.WhenAnyValue(x => x.IsAudioNotInitialized),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        PlayAudioCommand = ReactiveCommandHelper.CreateSafeCommand(
            PlayAudioAsync,
            canExecute: this.WhenAnyValue(
                x => x.IsAudioInitialized,
                x => x.IsWavePlayerPaused,
                x => x.IsWavePlayerStopped,
                (initialized, paused, stopped) => initialized && (paused || stopped)),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        PauseAudioCommand = ReactiveCommandHelper.CreateSafeCommand(
            PauseAudioAsync,
            canExecute: this.WhenAnyValue(
                x => x.IsAudioInitialized,
                x => x.IsWavePlayerPlaying,
                (initialized, playing) => initialized && playing),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        StopAudioCommand = ReactiveCommandHelper.CreateSafeCommand(
            StopAudioAsync,
            canExecute: this.WhenAnyValue(
                x => x.IsAudioInitialized,
                x => x.IsWavePlayerPlaying,
                x => x.IsWavePlayerPaused,
                (initialized, playing, paused) => initialized && (playing || paused)),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        PlayCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                await PlaySoundAsync(SelectedSoundTest);
            },
            canExecute: this.WhenAnyValue(
                x => x.IsAudioInitialized,
                x => x.IsWavePlayerPlaying,
                (initialized, playing) => initialized && playing),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        StopCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                await StopSoundAsync(SelectedSoundTest);
            },
            canExecute: this.WhenAnyValue(
                x => x.IsAudioInitialized,
                x => x.IsWavePlayerPlaying,
                (initialized, playing) => initialized && playing),
            outputScheduler: RxSchedulers.MainThreadScheduler);


        PlaySynthCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                await PlaySynthSoundAsync();
            },
            canExecute: this.WhenAnyValue(
                x => x.IsAudioInitialized,
                x => x.IsWavePlayerPlaying,
                (initialized, playing) => initialized && playing),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        StartSynthReleaseCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                await StartSynthReleaseAsync();
            },
            canExecute: this.WhenAnyValue(
                x => x.IsAudioInitialized,
                x => x.IsWavePlayerPlaying,
                (initialized, playing) => initialized && playing),
            outputScheduler: RxSchedulers.MainThreadScheduler);

        StopSynthCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                await StopSynthSoundAsync();
            },
            canExecute: this.WhenAnyValue(
                x => x.IsAudioInitialized,
                x => x.IsWavePlayerPlaying,
                (initialized, playing) => initialized && playing),
            outputScheduler: RxSchedulers.MainThreadScheduler);

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

    // NAudio initialization
    private bool _isAudioInitialized = false;
    public bool IsAudioInitialized
    {
        get => _isAudioInitialized;
        private set
        {
            if (_isAudioInitialized == value)
                return;

            this.RaiseAndSetIfChanged(ref _isAudioInitialized, value);
            this.RaisePropertyChanged(nameof(IsAudioNotInitialized));
        }
    }
    public bool IsAudioNotInitialized => !IsAudioInitialized;

    public bool IsWavePlayerPlaying => _wavePlayer.PlaybackState == PlaybackState.Playing;
    public bool IsWavePlayerPaused => _wavePlayer.PlaybackState == PlaybackState.Paused;
    public bool IsWavePlayerStopped => _wavePlayer.PlaybackState == PlaybackState.Stopped;


    private async Task InitAudioAsync()
    {
        // Setup audio rendering pipeline: Mixer -> Volume -> WavePlayer
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        _mixer = new MixingSampleProvider(waveFormat) { ReadFully = true }; // Always produce samples
        _volumeSampleProvider = new VolumeSampleProvider(_mixer);
        _volumeSampleProvider.Volume = 0.2f;   // 0.0 to 1.0

        _wavePlayer.Init(_volumeSampleProvider);

        IsAudioInitialized = true;

        StatusMessage = "Audio initialized.";
    }

    private async Task PlayAudioAsync()
    {
        _wavePlayer.Play();
        this.RaisePropertyChanged(nameof(IsWavePlayerPlaying));
        this.RaisePropertyChanged(nameof(IsWavePlayerPaused));
        this.RaisePropertyChanged(nameof(IsWavePlayerStopped));

        StatusMessage = "Audio playback started.";
    }

    private async Task PauseAudioAsync()
    {
        _wavePlayer.Pause();
        this.RaisePropertyChanged(nameof(IsWavePlayerPlaying));
        this.RaisePropertyChanged(nameof(IsWavePlayerPaused));
        this.RaisePropertyChanged(nameof(IsWavePlayerStopped));

        StatusMessage = "Audio playback paused.";
    }

    // NAudio stop all output
    private async Task StopAudioAsync()
    {
        _wavePlayer.Stop();

        var mixer = _mixer;
        if (mixer == null)
            return;

        var synthEnvelopProvider = _synthEnvelopProvider;
        if (synthEnvelopProvider != null)
            mixer.RemoveMixerInput(synthEnvelopProvider);
        _synthEnvelopProvider = null;

        var customSineWaveProvider = _customSineWaveProvider;
        if (customSineWaveProvider != null)
            mixer.RemoveMixerInput(customSineWaveProvider);
        _customSineWaveProvider = null;

        this.RaisePropertyChanged(nameof(IsWavePlayerPlaying));
        this.RaisePropertyChanged(nameof(IsWavePlayerPaused));
        this.RaisePropertyChanged(nameof(IsWavePlayerStopped));

        StatusMessage = "Audio playback stopped.";
    }

    // ------------------------------------------------------------------
    // Start of SynthEnvelopeProvider test
    // ------------------------------------------------------------------
    private SynthEnvelopeProvider? _synthEnvelopProvider = default!;
    private async Task PlaySynthSoundAsync()
    {
        var mixer = _mixer;
        if (mixer == null)
            return;

        var synthEnvelopProvider = _synthEnvelopProvider;
        if (synthEnvelopProvider == null)
        {
            //_soundProvider = new SynthEnvelopeProvider(SignalGeneratorType.SawTooth);
            synthEnvelopProvider = new SynthEnvelopeProvider(
                SignalGeneratorType.Triangle,
                sampleRate: 48000);
            _synthEnvelopProvider = synthEnvelopProvider;
        }
        synthEnvelopProvider.ResetADSR();

        mixer.RemoveMixerInput(synthEnvelopProvider);
        mixer.AddMixerInput(synthEnvelopProvider);

        synthEnvelopProvider.Frequency = 240.0f;

        synthEnvelopProvider.AttackSeconds = 0.2f;
        synthEnvelopProvider.DecaySeconds = 0.5f;
        synthEnvelopProvider.SustainLevel = 1.0f;
        synthEnvelopProvider.ReleaseSeconds = 2.0f;

        synthEnvelopProvider.StartAttack();

        StatusMessage = "Synth attack phase triggered";

        // Schedule release
        //int releaseDelaySeconds = (int)(_soundProvider.AttackSeconds + _soundProvider.DecaySeconds);
        //int releaseDelaySeconds = 2;

        //Task.Delay(releaseDelaySeconds * 1000).ContinueWith((_) =>
        //{
        //    Debug.WriteLine("StartRelease");
        //    _soundProvider.StartRelease();
        //});

        //await ReleaseAtDelay(releaseDelaySeconds * 1000);
    }
    private async Task ReleaseAtDelayAsync(int delayMs = 1000)
    {
        await Task.Delay(delayMs);
        if (_synthEnvelopProvider == null)
            return;
        _synthEnvelopProvider.StartRelease();
        StatusMessage = "Synth release phase triggered";
    }
    private async Task StartSynthReleaseAsync()
    {
        if (_synthEnvelopProvider == null)
            return;
        _synthEnvelopProvider.StartRelease();
        StatusMessage = "Synth release phase triggered";
    }

    private async Task StopSynthSoundAsync()
    {
        var mixer = _mixer;
        if (mixer == null)
            return;

        var synthEnvelopProvider = _synthEnvelopProvider;
        if (synthEnvelopProvider == null)
            return;
        synthEnvelopProvider.ResetADSR();
        mixer.RemoveMixerInput(synthEnvelopProvider);
        StatusMessage = "Stop playing Synth";
    }

    // ------------------------------------------------------------------
    // Start of SineWaveProvider32 test
    // ------------------------------------------------------------------
    private ISampleProvider? _customSineWaveProvider = default!;


    public SoundTest[] SoundTestValues { get; } = Enum.GetValues<SoundTest>();

    private async Task PlaySoundAsync(SoundTest soundTest)
    {
        switch (soundTest)
        {
            case SoundTest.TestTone:
                await PlaySineWaveAsync();
                break;
            case SoundTest.WaveformSynthesis:
                break;
            default:
                break;
        }
    }

    private async Task StopSoundAsync(SoundTest soundTest)
    {
        switch (soundTest)
        {
            case SoundTest.TestTone:
                await StopSineWaveAsync();
                break;
            case SoundTest.WaveformSynthesis:
                break;
            default:
                break;
        }
    }

    private async Task PlaySineWaveAsync()
    {
        var mixer = _mixer;
        if (mixer == null)
            return;

        if (_customSineWaveProvider != null)
            return;
        var customSineWaveProvider = new SineWaveProvider32().ToSampleProvider();
        _customSineWaveProvider = customSineWaveProvider;
        mixer.AddMixerInput(customSineWaveProvider);

        StatusMessage = "Playing Sine Wave (440 Hz).";
    }

    private async Task StopSineWaveAsync()
    {
        var mixer = _mixer;
        if (mixer == null)
            return;

        var customSineWaveProvider = _customSineWaveProvider;
        if (customSineWaveProvider == null)
            return;
        mixer.RemoveMixerInput(customSineWaveProvider);
        _customSineWaveProvider = null;
        StatusMessage = "Stop playing Sine Wave";
    }

    public class SineWaveProvider32 : IWaveProvider
    {
        public WaveFormat WaveFormat { get; }

        public SineWaveProvider32()
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count / 4; i++)
            {
                float sample = (float)Math.Sin((2 * Math.PI * 440 * i) / WaveFormat.SampleRate);
                buffer[i * 4] = buffer[i * 4 + 2] = (byte)(sample * byte.MaxValue);
                buffer[i * 4 + 1] = buffer[i * 4 + 3] = (byte)(sample * byte.MaxValue);
            }
            return count;
        }
    }
    // ------------------------------------------------------------------
    // End of SineWaveProvider32 test
    // ------------------------------------------------------------------
}
