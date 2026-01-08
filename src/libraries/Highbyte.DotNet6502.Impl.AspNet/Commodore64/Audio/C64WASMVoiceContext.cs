using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Audio;

public class C64WASMVoiceContext
{

    private C64WASMAudioHandler _audioHandler = default!;
    internal GainNodeSync? GainNode { get; private set; }

    internal WASMAudioHandlerContext AudioHandlerContext => _audioHandler.AudioHandlerContext!;
    private AudioContextSync _audioContext => AudioHandlerContext.AudioContext;

    private Action<string, int?, SidVoiceWaveForm?, AudioVoiceStatus?> _addDebugMessage = default!;

    internal void AddDebugMessage(string msg)
    {
        _addDebugMessage(msg, _voice, CurrentSidVoiceWaveForm, Status);
    }

    private readonly byte _voice;
    public byte Voice => _voice;
    public AudioVoiceStatus Status = AudioVoiceStatus.Stopped;
    public SidVoiceWaveForm CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;

    // SID Triangle Oscillator
    public C64WASMTriangleOscillator C64WASMTriangleOscillator { get; private set; } = default!;

    // SID Sawtooth Oscillator
    public C64WASMSawToothOscillator C64WASMSawToothOscillator { get; private set; } = default!;

    // SID pulse oscillator
    public C64WASMPulseOscillator C64WASMPulseOscillator { get; private set; } = default!;

    // SID noise oscillator
    public C64WASMNoiseOscillator C64WASMNoiseOscillator { get; private set; } = default!;

    private EventListener<EventSync> _audioStoppedCallback = default!;

#pragma warning disable IDE0052 // Remove unread private members
    private Timer _adsCycleCompleteTimer = default!;
    private Timer _releaseCycleCompleteTimer = default!;
#pragma warning restore IDE0052 // Remove unread private members

    //private readonly SemaphoreSlim _semaphoreSlim = new(1);
    //public SemaphoreSlim SemaphoreSlim => _semaphoreSlim;

    public C64WASMVoiceContext(byte voice)
    {
        _voice = voice;
    }

    internal void Init(
        C64WASMAudioHandler audioHandler,
        Action<string, int?, SidVoiceWaveForm?, AudioVoiceStatus?> addDebugMessage)
    {
        Status = AudioVoiceStatus.Stopped;

        _audioHandler = audioHandler;
        _addDebugMessage = addDebugMessage;

        // Create gain node to use for a specfic voice. Used internally to be able to turn off audio without stopping the oscillator.
        GainNode = GainNodeSync.Create(_audioContext.JSRuntime, _audioContext);
        // Connect the gain node to the common SID volume gain node
        GainNode.Connect(_audioHandler.CommonSIDGainNode);

        // Create implementations of the different oscillators
        C64WASMTriangleOscillator = new C64WASMTriangleOscillator(this);
        C64WASMSawToothOscillator = new C64WASMSawToothOscillator(this);
        C64WASMPulseOscillator = new C64WASMPulseOscillator(this);
        C64WASMNoiseOscillator = new C64WASMNoiseOscillator(this);

        if (_audioHandler.StopAndRecreateOscillator)
        {
            // Define callback handler to know when an oscillator has stopped playing. Only used if creating + starting oscillators before each audio.
            _audioStoppedCallback = EventListener<EventSync>.Create(_audioContext.WebAudioHelper, _audioContext.JSRuntime, (e) =>
            {
                AddDebugMessage($"Oscillator Stop Callback triggered.");
                Stop();
            });
        }
        else
        {
            // Unless we won't recreate/start the oscillator before each audio, create and start oscillators in advance 
            foreach (var sidWaveFormType in Enum.GetValues<SidVoiceWaveForm>())
            {
                var audioVoiceParameter = new AudioVoiceParameter
                {
                    SIDOscillatorType = sidWaveFormType,
                    Frequency = 300f,
                    PulseWidth = -0.22f,
                };
                CreateOscillator(audioVoiceParameter);
                //ConnectOscillator(audioVoiceParameter.SIDOscillatorType);

                // In this scenario the WebAudio oscilltor has to be running all the time (it can only be started/stopped once).
                // As the gain is 0, no sound will play. When a SID sound started to play, a ADS envelope (gain variation over time) is scheduled on the current time.
                StartOscillator(audioVoiceParameter.SIDOscillatorType);
            }
        }

        CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;
    }

    private void ScheduleAudioStopAfterDecay(int waitMs)
    {
        // Set timer to stop audio after a while via a .NET timer
        _adsCycleCompleteTimer = new Timer((_) =>
        {
            AddDebugMessage($"Scheduled Stop after Decay triggered.");
            Stop();
        }, null, waitMs, Timeout.Infinite);
    }

    private void ScheduleAudioStopAfterRelease(double releaseDurationSeconds)
    {
        AddDebugMessage($"Scheduling voice stop at now + {releaseDurationSeconds} seconds.");

        // Schedule Stop for oscillator and other audio sources) when the Release period if over
        //voiceContext.Oscillator?.Stop(currentTime + audioVoiceParameter.ReleaseDurationSeconds);
        //voiceContext.PulseOscillator?.Stop(currentTime + audioVoiceParameter.ReleaseDurationSeconds);
        //voiceContext.NoiseGenerator?.Stop(currentTime + audioVoiceParameter.ReleaseDurationSeconds);

        var waitMs = (int)(releaseDurationSeconds * 1000.0d);
        // Set timer to stop audio after a while via a .NET timer
        _releaseCycleCompleteTimer = new Timer((_) =>
        {
            AddDebugMessage($"Scheduled Stop after Release triggered.");
            Stop();
        }, null, waitMs, Timeout.Infinite);
    }

    internal void Stop()
    {
        AddDebugMessage($"Stop issued");

        if (_audioHandler.StopAndRecreateOscillator)
        {
            // This is called either via callback when oscillator sent "ended" event, or manually stopped via turning off SID gate.
            if (Status != AudioVoiceStatus.Stopped)
                StopOscillatorNow(CurrentSidVoiceWaveForm);
            CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;
        }
        else
        {
            // In this scenario, the oscillator is still running. Set volume to 0 in the CommonSIDGainNode to ensure no audio is playing. 
            AddDebugMessage($"Cancelling current CommonSIDGainNode schedule");
            var gainAudioParam = GainNode!.GetGain();
            var currentTime = _audioContext.GetCurrentTime();
            gainAudioParam.CancelScheduledValues(currentTime);
            gainAudioParam.SetValueAtTime(0, currentTime);

            // If configured, disconnect the oscillator when stopping
            if (_audioHandler.DisconnectOscillatorOnStop)
            {
                DisconnectOscillator(CurrentSidVoiceWaveForm);
                CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;
            }
        }

        if (Status != AudioVoiceStatus.Stopped)
        {
            Status = AudioVoiceStatus.Stopped;
            AddDebugMessage($"Status changed.");
        }
        else
        {
            AddDebugMessage($"Status already was Stopped");
        }
    }

    internal void StopAllOscillatorsNow()
    {
        foreach (var sidWaveFormType in Enum.GetValues<SidVoiceWaveForm>())
        {
            StopOscillatorNow(sidWaveFormType);
        }
    }

    private void StopOscillatorNow(SidVoiceWaveForm sidVoiceWaveForm)
    {
        AddDebugMessage($"Stopping oscillator: {sidVoiceWaveForm}");

        // Switch on sidVoiceForm
        switch (sidVoiceWaveForm)
        {
            case SidVoiceWaveForm.None:
                break;
            case SidVoiceWaveForm.Triangle:
                C64WASMTriangleOscillator?.StopNow();
                break;
            case SidVoiceWaveForm.Sawtooth:
                C64WASMSawToothOscillator?.StopNow();
                break;
            case SidVoiceWaveForm.Pulse:
                C64WASMPulseOscillator?.StopNow();
                break;
            case SidVoiceWaveForm.RandomNoise:
                C64WASMNoiseOscillator?.StopNow();
                break;
            default:
                break;
        }
    }

    private void StopOscillatorLater(SidVoiceWaveForm sidVoiceWaveForm, double when)
    {
        AddDebugMessage($"Stopping oscillator: {sidVoiceWaveForm} later");

        // Switch on sidVoiceForm
        switch (sidVoiceWaveForm)
        {
            case SidVoiceWaveForm.None:
                break;
            case SidVoiceWaveForm.Triangle:
                C64WASMTriangleOscillator?.StopLater(when);
                break;
            case SidVoiceWaveForm.Sawtooth:
                C64WASMSawToothOscillator?.StopLater(when);
                break;
            case SidVoiceWaveForm.Pulse:
                C64WASMPulseOscillator?.StopLater(when);
                break;
            case SidVoiceWaveForm.RandomNoise:
                C64WASMNoiseOscillator?.StopLater(when);
                break;
            default:
                break;
        }
    }

    private void ConnectOscillator(SidVoiceWaveForm sidVoiceWaveForm)
    {
        AddDebugMessage($"Connecting oscillator: {sidVoiceWaveForm}");
        switch (sidVoiceWaveForm)
        {
            case SidVoiceWaveForm.None:
                break;
            case SidVoiceWaveForm.Triangle:
                C64WASMTriangleOscillator?.Connect();
                break;
            case SidVoiceWaveForm.Sawtooth:
                C64WASMSawToothOscillator?.Connect();
                break;
            case SidVoiceWaveForm.Pulse:
                C64WASMPulseOscillator?.Connect();
                break;
            case SidVoiceWaveForm.RandomNoise:
                C64WASMNoiseOscillator?.Connect();
                break;
            default:
                break;
        }
    }

    private void DisconnectOscillator(SidVoiceWaveForm sidVoiceWaveForm)
    {
        AddDebugMessage($"Disconnecting oscillator: {sidVoiceWaveForm}");

        // Switch on sidVoiceForm
        switch (sidVoiceWaveForm)
        {
            case SidVoiceWaveForm.None:
                break;
            case SidVoiceWaveForm.Triangle:
                C64WASMTriangleOscillator?.Disconnect();
                break;
            case SidVoiceWaveForm.Sawtooth:
                C64WASMSawToothOscillator?.Disconnect();
                break;
            case SidVoiceWaveForm.Pulse:
                C64WASMPulseOscillator?.Disconnect();
                break;
            case SidVoiceWaveForm.RandomNoise:
                C64WASMNoiseOscillator?.Disconnect();
                break;
            default:
                break;
        }
    }

    private void CreateOscillator(AudioVoiceParameter audioVoiceParameter)
    {
        AddDebugMessage($"Creating oscillator: {audioVoiceParameter.SIDOscillatorType}");

        switch (audioVoiceParameter.SIDOscillatorType)
        {
            case SidVoiceWaveForm.None:
                break;
            case SidVoiceWaveForm.Triangle:
                C64WASMTriangleOscillator?.Create(audioVoiceParameter.Frequency);
                if (_audioHandler.StopAndRecreateOscillator)
                    C64WASMTriangleOscillator!.TriangleOscillator!.AddEndedEventListsner(_audioStoppedCallback);
                break;
            case SidVoiceWaveForm.Sawtooth:
                C64WASMSawToothOscillator?.Create(audioVoiceParameter.Frequency);
                if (_audioHandler.StopAndRecreateOscillator)
                    C64WASMSawToothOscillator!.SawToothOscillator!.AddEndedEventListsner(_audioStoppedCallback);
                break;
            case SidVoiceWaveForm.Pulse:
                C64WASMPulseOscillator?.Create(audioVoiceParameter.Frequency, audioVoiceParameter.PulseWidth);
                if (_audioHandler.StopAndRecreateOscillator)
                    C64WASMPulseOscillator!.PulseOscillator!.AddEndedEventListsner(_audioStoppedCallback);
                break;
            case SidVoiceWaveForm.RandomNoise:
                var playbackRate = C64WASMNoiseOscillator.GetPlaybackRateFromFrequency(audioVoiceParameter.Frequency);
                C64WASMNoiseOscillator?.Create(playbackRate);
                if (_audioHandler.StopAndRecreateOscillator)
                    C64WASMNoiseOscillator!.NoiseGenerator!.AddEndedEventListsner(_audioStoppedCallback);
                break;
            default:
                break;
        }
    }

    private void StartOscillator(SidVoiceWaveForm sidVoiceWaveForm)
    {
        AddDebugMessage($"Starting oscillator: {sidVoiceWaveForm}");

        switch (sidVoiceWaveForm)
        {
            case SidVoiceWaveForm.None:
                break;
            case SidVoiceWaveForm.Triangle:
                C64WASMTriangleOscillator?.Start();
                break;
            case SidVoiceWaveForm.Sawtooth:
                C64WASMSawToothOscillator?.Start();
                break;
            case SidVoiceWaveForm.Pulse:
                C64WASMPulseOscillator?.Start();
                break;
            case SidVoiceWaveForm.RandomNoise:
                C64WASMNoiseOscillator?.Start();
                break;
            default:
                break;
        }
    }

    private void SetOscillatorParameters(AudioVoiceParameter audioVoiceParameter, double currentTime)
    {
        AddDebugMessage($"Setting oscillator parameters: {audioVoiceParameter.SIDOscillatorType}");

        switch (audioVoiceParameter.SIDOscillatorType)
        {
            case SidVoiceWaveForm.None:
                // Set frequency
                SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency, currentTime);
                break;
            case SidVoiceWaveForm.Triangle:
                // Set frequency 
                SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency, currentTime);
                break;
            case SidVoiceWaveForm.Sawtooth:
                // Set frequency
                SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency, currentTime);
                break;
            case SidVoiceWaveForm.Pulse:
                // Set frequency 
                SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency, currentTime);
                // Set pulsewidth
                C64WASMPulseOscillator.SetPulseWidth(audioVoiceParameter.PulseWidth, currentTime);
                // Set Pulse Width ADSR
                C64WASMPulseOscillator.SetPulseWidthDepthADSR(currentTime);
                break;
            case SidVoiceWaveForm.RandomNoise:
                // Set frequency (playback rate) on current NoiseGenerator
                SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency, currentTime);
                break;
            default:
                break;
        }
    }

    private void SwitchOscillatorConnection(SidVoiceWaveForm newSidVoiceWaveForm)
    {
        // If current oscillator is the same as the requested one, do nothing (assume it's already connected)
        if (newSidVoiceWaveForm == CurrentSidVoiceWaveForm)
            return;

        // If any other oscillator is currently connected
        if (CurrentSidVoiceWaveForm != SidVoiceWaveForm.None)
            // Stop any existing playing audio will also disconnect it's oscillator
            Stop();

        // Then connect the specified oscillator
        ConnectOscillator(newSidVoiceWaveForm);

        // Remember the new current oscillator
        CurrentSidVoiceWaveForm = newSidVoiceWaveForm;
    }

    internal void StartAudioADSPhase(AudioVoiceParameter audioVoiceParameter)
    {
        var currentTime = _audioContext.GetCurrentTime();

        if (_audioHandler.StopAndRecreateOscillator)
        {
            // 1. Stop current oscillator (if any) and release it's resoruces.
            // 2. Create new oscillator (even if same as before)
            //      With parameters such as Frequency, PulseWidth, etc.
            //      With Callback when ADSR envelope is finished to stop audio by stopping the oscillator (which then cannot be used anymore)
            // 3. Connect oscillator to gain node
            // 4. Set Gain ADSR envelope
            // 5. Start oscillator -> This will start the audio

            StopOscillatorNow(CurrentSidVoiceWaveForm);
            CurrentSidVoiceWaveForm = audioVoiceParameter.SIDOscillatorType;
            CreateOscillator(audioVoiceParameter);
            ConnectOscillator(CurrentSidVoiceWaveForm);
            SetGainADS(audioVoiceParameter, currentTime);
            StartOscillator(CurrentSidVoiceWaveForm);
        }
        else
        {
            // Assume oscillator is already created and started
            // 1. Connect oscillator to gain node (and disconnect previous oscillator if different)
            // 2. Set parameters on existing oscillator such as Frequency, PulseWidth, etc.
            // 3. Set Gain ADSR envelope -> This will start the audio
            // 4. Set Callback to stop audio by setting Gain to 0 when envelope is finished

            SwitchOscillatorConnection(audioVoiceParameter.SIDOscillatorType);
            SetOscillatorParameters(audioVoiceParameter, currentTime);
            SetGainADS(audioVoiceParameter, currentTime);

            // If SustainGain is 0, then we need to schedule a stop of the audio
            // when the attack + decay period is over.
            if (audioVoiceParameter.SustainGain == 0)
            {
                var waitSeconds = audioVoiceParameter.AttackDurationSeconds + audioVoiceParameter.DecayDurationSeconds;
                AddDebugMessage($"Scheduling voice stop now + {waitSeconds} seconds.");
                ScheduleAudioStopAfterDecay(waitMs: (int)(waitSeconds * 1000.0d));
            }
        }

        Status = AudioVoiceStatus.ADSCycleStarted;
        AddDebugMessage($"Status changed");
    }

    internal void StartAudioReleasePhase(AudioVoiceParameter audioVoiceParameter)
    {
        var currentTime = _audioContext.GetCurrentTime();
        SetGainRelease(audioVoiceParameter, currentTime);

        if (_audioHandler.StopAndRecreateOscillator)
        {
            // Plan oscillator built-in delayed stop with callback
            StopOscillatorLater(CurrentSidVoiceWaveForm, currentTime + audioVoiceParameter.ReleaseDurationSeconds);
        }
        else
        {
            // Plan manual callback after release duration (as we don't stop the oscillator in this scenario, as it cannot be started again)
            ScheduleAudioStopAfterRelease(audioVoiceParameter.ReleaseDurationSeconds);
        }

        Status = AudioVoiceStatus.ReleaseCycleStarted;
        AddDebugMessage($"Status changed");
    }

    private void SetGainADS(AudioVoiceParameter audioVoiceParameter, double currentTime)
    {
        AddDebugMessage($"Setting Attack ({audioVoiceParameter.AttackDurationSeconds}) Decay ({audioVoiceParameter.DecayDurationSeconds}) Sustain ({audioVoiceParameter.SustainGain})");

        // Set Attack/Decay/Sustain gain envelope
        var gainAudioParam = GainNode!.GetGain();
        gainAudioParam.CancelScheduledValues(currentTime);
        gainAudioParam.SetValueAtTime(0, currentTime);
        gainAudioParam.LinearRampToValueAtTime(1.0f, currentTime + audioVoiceParameter.AttackDurationSeconds);
        gainAudioParam.LinearRampToValueAtTime(audioVoiceParameter.SustainGain, currentTime + audioVoiceParameter.AttackDurationSeconds + audioVoiceParameter.DecayDurationSeconds);
        //gainAudioParam.SetTargetAtTime(audioVoiceParameter.SustainGain, currentTime + audioVoiceParameter.AttackDurationSeconds, audioVoiceParameter.DecayDurationSeconds);
    }

    private void SetGainRelease(AudioVoiceParameter audioVoiceParameter, double currentTime)
    {
        AddDebugMessage($"Setting Gain Release ({audioVoiceParameter.ReleaseDurationSeconds})");

        // Schedule a volume change from current gain level down to 0 during specified Release time 
        var gainAudioParam = GainNode!.GetGain();
        var currentGainValue = gainAudioParam.GetCurrentValue();
        gainAudioParam.CancelScheduledValues(currentTime);
        gainAudioParam.SetValueAtTime(currentGainValue, currentTime);
        gainAudioParam.LinearRampToValueAtTime(0, currentTime + audioVoiceParameter.ReleaseDurationSeconds);
    }

    internal void SetFrequencyOnCurrentOscillator(float frequency, double changeTime)
    {
        // Noise sample generator
        if (CurrentSidVoiceWaveForm == SidVoiceWaveForm.RandomNoise)
        {
            var playbackRate = C64WASMNoiseOscillator.GetPlaybackRateFromFrequency(frequency);
            var playbackRateAudioParam = C64WASMNoiseOscillator.NoiseGenerator!.GetPlaybackRate();

            // Check if the playback rate of the actual audio buffer source is different from the new rate
            // TODO: Is this necessary to check? Could the rate have been changed in other way?
            var currentPlaybackRateValue = playbackRateAudioParam.GetCurrentValue();
            if (currentPlaybackRateValue != playbackRate)
            {
                AddDebugMessage($"Changing playback rate to {playbackRate} based on freq {frequency}");
                playbackRateAudioParam.SetValueAtTime(playbackRate, changeTime);
            }
            return;
        }

        // Normal oscillators
        AudioParamSync frequencyAudioParam;
        switch (CurrentSidVoiceWaveForm)
        {
            case SidVoiceWaveForm.None:
                return;
            case SidVoiceWaveForm.Triangle:
                frequencyAudioParam = C64WASMTriangleOscillator!.TriangleOscillator!.GetFrequency();
                break;
            case SidVoiceWaveForm.Sawtooth:
                frequencyAudioParam = C64WASMSawToothOscillator!.SawToothOscillator!.GetFrequency();
                break;
            case SidVoiceWaveForm.Pulse:
                frequencyAudioParam = C64WASMPulseOscillator!.PulseOscillator!.GetFrequency();
                break;
            default:
                throw new NotImplementedException();
        }

        // Check if the frequency of the actual oscillator is different from the new frequency
        // TODO: Is this necessary to check? Could the frequency have been changed in other way?
        var currentFrequencyValue = frequencyAudioParam.GetCurrentValue();
        if (currentFrequencyValue != frequency)
        {
            // DEBUG START
            //var gainAudioParam = CommonSIDGainNode!.GetGain();
            //var currentGainValue = gainAudioParam.GetCurrentValue();
            // END DEBUG

            AddDebugMessage($"Changing freq to {frequency}.");
            frequencyAudioParam.SetValueAtTime(frequency, changeTime);

            // DEBUG START
            // currentGainValue = gainAudioParam.GetCurrentValue();
            // END DEBUG
        }
    }
}
