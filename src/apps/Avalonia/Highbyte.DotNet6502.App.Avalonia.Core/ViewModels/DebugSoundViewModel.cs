using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
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

        CloseCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                CloseRequested?.Invoke(this, true);
            },
            outputScheduler: RxApp.MainThreadScheduler);

        InitAudioCommand = ReactiveCommand.CreateFromTask(
            InitAudio,
            this.WhenAnyValue(x => x.IsAudioNotInitialized),
            outputScheduler: RxApp.MainThreadScheduler);

        PlayAudioCommand = ReactiveCommand.CreateFromTask(
            PlayAudio,
            this.WhenAnyValue(x => x.IsAudioInitialized),
            outputScheduler: RxApp.MainThreadScheduler);

        PauseAudioCommand = ReactiveCommand.CreateFromTask(
            PauseAudio,
            this.WhenAnyValue(x => x.IsAudioInitialized),
            outputScheduler: RxApp.MainThreadScheduler);

        StopAudioCommand = ReactiveCommand.CreateFromTask(
            StopAudio,
            this.WhenAnyValue(x => x.IsAudioInitialized),
            outputScheduler: RxApp.MainThreadScheduler);

        PlayCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                await PlaySound(SelectedSoundTest);
            },
            this.WhenAnyValue(x => x.IsAudioInitialized),
            outputScheduler: RxApp.MainThreadScheduler);

        StopCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                await StopSound(SelectedSoundTest);
            },
            this.WhenAnyValue(x => x.IsAudioInitialized),
            outputScheduler: RxApp.MainThreadScheduler);


        PlaySynthCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                await PlaySynthSound();
            },
            this.WhenAnyValue(x => x.IsAudioInitialized),
            outputScheduler: RxApp.MainThreadScheduler);

        StartSynthReleaseCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                await StartSynthRelease();
            },
            this.WhenAnyValue(x => x.IsAudioInitialized),
            outputScheduler: RxApp.MainThreadScheduler);

        StopSynthCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                await StopSynthSound();
            },
            this.WhenAnyValue(x => x.IsAudioInitialized),
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

    private async Task PlaySound(SoundTest soundTest)
    {
        switch (soundTest)
        {
            case SoundTest.TestTone:
                await PlaySineWave();
                break;
            case SoundTest.WaveformSynthesis:
                break;
            default:
                break;
        }
    }

    private async Task StopSound(SoundTest soundTest)
    {
        switch (soundTest)
        {
            case SoundTest.TestTone:
                await StopSineWave();
                break;
            case SoundTest.WaveformSynthesis:
                break;
            default:
                break;
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


    private async Task InitAudio()
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

    private async Task PlayAudio()
    {
        _wavePlayer.Play();
        StatusMessage = "Audio playback started.";
    }

    private async Task PauseAudio()
    {
        _wavePlayer.Pause();
        StatusMessage = "Audio playback paused.";
    }

    // NAudio stop all output
    private async Task StopAudio()
    {
        _wavePlayer.Stop();
        for (int i = 0; i < _mixer.MixerInputs.Count(); i++)
        {
            var mixerInput = _mixer.MixerInputs.First();
            _mixer.RemoveMixerInput(mixerInput);
        }
        _mixer = null;

        _volumeSampleProvider = null;
        _synthEnvelopProvider = null;
        _customSineWaveProvider = null;

        StatusMessage = "Audio playback stopped.";
    }

    // ------------------------------------------------------------------
    // Start of SynthEnvelopeProvider test
    // ------------------------------------------------------------------
    private SynthEnvelopeProvider? _synthEnvelopProvider = default!;
    private async Task PlaySynthSound()
    {
        if (_synthEnvelopProvider == null)
        {
            //_soundProvider = new SynthEnvelopeProvider(SignalGeneratorType.SawTooth);
            _synthEnvelopProvider = new SynthEnvelopeProvider(
                SignalGeneratorType.Triangle,
                sampleRate: 48000);

            // Setup audio rendering pipeline: soundProvider -> Mixer -> Volume -> WavePlayer
            _mixer.AddMixerInput(_synthEnvelopProvider);
        }
        else
        {
            _synthEnvelopProvider.ResetADSR();
        }

        _synthEnvelopProvider.Frequency = 240.0f;

        _synthEnvelopProvider.AttackSeconds = 0.2f;
        _synthEnvelopProvider.DecaySeconds = 0.5f;
        _synthEnvelopProvider.SustainLevel = 1.0f;
        _synthEnvelopProvider.ReleaseSeconds = 2.0f;

        _synthEnvelopProvider.StartAttack();

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
    private async Task ReleaseAtDelay(int delayMs = 1000)
    {
        await Task.Delay(delayMs);
        if (_synthEnvelopProvider == null)
            return;
        _synthEnvelopProvider.StartRelease();
        StatusMessage = "Synth release phase triggered";
    }
    private async Task StartSynthRelease()
    {
        if (_synthEnvelopProvider == null)
            return;
        _synthEnvelopProvider.StartRelease();
        StatusMessage = "Synth release phase triggered";
    }

    private async Task StopSynthSound()
    {
        if (_synthEnvelopProvider == null)
            return;
        _mixer.RemoveMixerInput(_synthEnvelopProvider);
        _synthEnvelopProvider = null;
        StatusMessage = "Stop playing Synth";
    }

    // ------------------------------------------------------------------
    // Start of SineWaveProvider32 test
    // ------------------------------------------------------------------
    private ISampleProvider? _customSineWaveProvider = default!;

    private async Task PlaySineWave()
    {
        if (_customSineWaveProvider != null)
            return;
        _customSineWaveProvider = new SineWaveProvider32().ToSampleProvider();
        _mixer.AddMixerInput(_customSineWaveProvider);

        StatusMessage = "Playing Sine Wave (440 Hz).";
    }

    private async Task StopSineWave()
    {
        if (_customSineWaveProvider == null)
            return;
        _mixer.RemoveMixerInput(_customSineWaveProvider);
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
