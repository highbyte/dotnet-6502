using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Systems.Audio;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio;

/// <summary>
/// One WebAudio synth voice for the command-stream audio style. System-agnostic — driven by the
/// host-neutral <see cref="AudioVoiceParameter"/> / <see cref="AudioOscillatorType"/> vocabulary.
/// </summary>
public class WebAudioVoiceContext
{

    // Supplied by the audio command target.
    private WASMAudioHandlerContext _audioHandlerContext = default!;
    private GainNodeSync _commonGainNode = default!;
    private bool _stopAndRecreateOscillator;
    private bool _disconnectOscillatorOnStop;

    internal GainNodeSync? GainNode { get; private set; }

    internal WASMAudioHandlerContext AudioHandlerContext => _audioHandlerContext;
    private AudioContextSync _audioContext => AudioHandlerContext.AudioContext;

    private Action<string, int?, AudioOscillatorType?, AudioVoiceStatus?> _addDebugMessage = default!;

    internal void AddDebugMessage(string msg)
    {
        _addDebugMessage(msg, _voice, CurrentOscillatorType, Status);
    }

    private readonly byte _voice;
    public byte Voice => _voice;
    public AudioVoiceStatus Status = AudioVoiceStatus.Stopped;
    public AudioOscillatorType CurrentOscillatorType = AudioOscillatorType.None;

    // Triangle Oscillator
    public WebAudioTriangleOscillator TriangleOscillator { get; private set; } = default!;

    // Sawtooth Oscillator
    public WebAudioSawToothOscillator SawToothOscillator { get; private set; } = default!;

    // Pulse oscillator
    public WebAudioPulseOscillator PulseOscillator { get; private set; } = default!;

    // Noise oscillator
    public WebAudioNoiseOscillator NoiseOscillator { get; private set; } = default!;

    private EventListener<EventSync> _audioStoppedCallback = default!;

#pragma warning disable IDE0052 // Remove unread private members
    private Timer _adsCycleCompleteTimer = default!;
    private Timer _releaseCycleCompleteTimer = default!;
#pragma warning restore IDE0052 // Remove unread private members

    public WebAudioVoiceContext(byte voice)
    {
        _voice = voice;
    }

    internal void Init(
        WASMAudioHandlerContext audioHandlerContext,
        GainNodeSync commonGainNode,
        bool stopAndRecreateOscillator,
        bool disconnectOscillatorOnStop,
        Action<string, int?, AudioOscillatorType?, AudioVoiceStatus?> addDebugMessage)
    {
        Status = AudioVoiceStatus.Stopped;

        _audioHandlerContext = audioHandlerContext;
        _commonGainNode = commonGainNode;
        _stopAndRecreateOscillator = stopAndRecreateOscillator;
        _disconnectOscillatorOnStop = disconnectOscillatorOnStop;
        _addDebugMessage = addDebugMessage;

        // Create gain node to use for a specfic voice. Used internally to be able to turn off audio without stopping the oscillator.
        GainNode = GainNodeSync.Create(_audioContext.JSRuntime, _audioContext);
        // Connect the gain node to the common volume gain node
        GainNode.Connect(_commonGainNode);

        // Create implementations of the different oscillators
        TriangleOscillator = new WebAudioTriangleOscillator(this);
        SawToothOscillator = new WebAudioSawToothOscillator(this);
        PulseOscillator = new WebAudioPulseOscillator(this);
        NoiseOscillator = new WebAudioNoiseOscillator(this);

        if (_stopAndRecreateOscillator)
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
            foreach (var oscillatorType in Enum.GetValues<AudioOscillatorType>())
            {
                var audioVoiceParameter = new AudioVoiceParameter
                {
                    OscillatorType = oscillatorType,
                    Frequency = 300f,
                    PulseWidth = -0.22f,
                };
                CreateOscillator(audioVoiceParameter);
                //ConnectOscillator(audioVoiceParameter.OscillatorType);

                // In this scenario the WebAudio oscilltor has to be running all the time (it can only be started/stopped once).
                // As the gain is 0, no sound will play. When a sound started to play, a ADS envelope (gain variation over time) is scheduled on the current time.
                StartOscillator(audioVoiceParameter.OscillatorType);
            }
        }

        CurrentOscillatorType = AudioOscillatorType.None;
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

        if (_stopAndRecreateOscillator)
        {
            // This is called either via callback when oscillator sent "ended" event, or manually stopped via turning off SID gate.
            if (Status != AudioVoiceStatus.Stopped)
                StopOscillatorNow(CurrentOscillatorType);
            CurrentOscillatorType = AudioOscillatorType.None;
        }
        else
        {
            // In this scenario, the oscillator is still running. Set volume to 0 in the GainNode to ensure no audio is playing.
            AddDebugMessage($"Cancelling current GainNode schedule");
            var gainAudioParam = GainNode!.GetGain();
            var currentTime = _audioContext.GetCurrentTime();
            gainAudioParam.CancelScheduledValues(currentTime);
            gainAudioParam.SetValueAtTime(0, currentTime);

            // If configured, disconnect the oscillator when stopping
            if (_disconnectOscillatorOnStop)
            {
                DisconnectOscillator(CurrentOscillatorType);
                CurrentOscillatorType = AudioOscillatorType.None;
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
        foreach (var oscillatorType in Enum.GetValues<AudioOscillatorType>())
        {
            StopOscillatorNow(oscillatorType);
        }
    }

    private void StopOscillatorNow(AudioOscillatorType oscillatorType)
    {
        AddDebugMessage($"Stopping oscillator: {oscillatorType}");

        switch (oscillatorType)
        {
            case AudioOscillatorType.None:
                break;
            case AudioOscillatorType.Triangle:
                TriangleOscillator?.StopNow();
                break;
            case AudioOscillatorType.Sawtooth:
                SawToothOscillator?.StopNow();
                break;
            case AudioOscillatorType.Pulse:
                PulseOscillator?.StopNow();
                break;
            case AudioOscillatorType.Noise:
                NoiseOscillator?.StopNow();
                break;
            default:
                break;
        }
    }

    private void StopOscillatorLater(AudioOscillatorType oscillatorType, double when)
    {
        AddDebugMessage($"Stopping oscillator: {oscillatorType} later");

        switch (oscillatorType)
        {
            case AudioOscillatorType.None:
                break;
            case AudioOscillatorType.Triangle:
                TriangleOscillator?.StopLater(when);
                break;
            case AudioOscillatorType.Sawtooth:
                SawToothOscillator?.StopLater(when);
                break;
            case AudioOscillatorType.Pulse:
                PulseOscillator?.StopLater(when);
                break;
            case AudioOscillatorType.Noise:
                NoiseOscillator?.StopLater(when);
                break;
            default:
                break;
        }
    }

    private void ConnectOscillator(AudioOscillatorType oscillatorType)
    {
        AddDebugMessage($"Connecting oscillator: {oscillatorType}");
        switch (oscillatorType)
        {
            case AudioOscillatorType.None:
                break;
            case AudioOscillatorType.Triangle:
                TriangleOscillator?.Connect();
                break;
            case AudioOscillatorType.Sawtooth:
                SawToothOscillator?.Connect();
                break;
            case AudioOscillatorType.Pulse:
                PulseOscillator?.Connect();
                break;
            case AudioOscillatorType.Noise:
                NoiseOscillator?.Connect();
                break;
            default:
                break;
        }
    }

    private void DisconnectOscillator(AudioOscillatorType oscillatorType)
    {
        AddDebugMessage($"Disconnecting oscillator: {oscillatorType}");

        switch (oscillatorType)
        {
            case AudioOscillatorType.None:
                break;
            case AudioOscillatorType.Triangle:
                TriangleOscillator?.Disconnect();
                break;
            case AudioOscillatorType.Sawtooth:
                SawToothOscillator?.Disconnect();
                break;
            case AudioOscillatorType.Pulse:
                PulseOscillator?.Disconnect();
                break;
            case AudioOscillatorType.Noise:
                NoiseOscillator?.Disconnect();
                break;
            default:
                break;
        }
    }

    private void CreateOscillator(AudioVoiceParameter audioVoiceParameter)
    {
        AddDebugMessage($"Creating oscillator: {audioVoiceParameter.OscillatorType}");

        switch (audioVoiceParameter.OscillatorType)
        {
            case AudioOscillatorType.None:
                break;
            case AudioOscillatorType.Triangle:
                TriangleOscillator?.Create(audioVoiceParameter.Frequency);
                if (_stopAndRecreateOscillator)
                    TriangleOscillator!.TriangleOscillator!.AddEndedEventListsner(_audioStoppedCallback);
                break;
            case AudioOscillatorType.Sawtooth:
                SawToothOscillator?.Create(audioVoiceParameter.Frequency);
                if (_stopAndRecreateOscillator)
                    SawToothOscillator!.SawToothOscillator!.AddEndedEventListsner(_audioStoppedCallback);
                break;
            case AudioOscillatorType.Pulse:
                PulseOscillator?.Create(audioVoiceParameter.Frequency, audioVoiceParameter.PulseWidth);
                if (_stopAndRecreateOscillator)
                    PulseOscillator!.PulseOscillator!.AddEndedEventListsner(_audioStoppedCallback);
                break;
            case AudioOscillatorType.Noise:
                var playbackRate = NoiseOscillator.GetPlaybackRateFromFrequency(audioVoiceParameter.Frequency);
                NoiseOscillator?.Create(playbackRate);
                if (_stopAndRecreateOscillator)
                    NoiseOscillator!.NoiseGenerator!.AddEndedEventListsner(_audioStoppedCallback);
                break;
            default:
                break;
        }
    }

    private void StartOscillator(AudioOscillatorType oscillatorType)
    {
        AddDebugMessage($"Starting oscillator: {oscillatorType}");

        switch (oscillatorType)
        {
            case AudioOscillatorType.None:
                break;
            case AudioOscillatorType.Triangle:
                TriangleOscillator?.Start();
                break;
            case AudioOscillatorType.Sawtooth:
                SawToothOscillator?.Start();
                break;
            case AudioOscillatorType.Pulse:
                PulseOscillator?.Start();
                break;
            case AudioOscillatorType.Noise:
                NoiseOscillator?.Start();
                break;
            default:
                break;
        }
    }

    private void SetOscillatorParameters(AudioVoiceParameter audioVoiceParameter, double currentTime)
    {
        AddDebugMessage($"Setting oscillator parameters: {audioVoiceParameter.OscillatorType}");

        switch (audioVoiceParameter.OscillatorType)
        {
            case AudioOscillatorType.None:
            case AudioOscillatorType.Triangle:
            case AudioOscillatorType.Sawtooth:
            case AudioOscillatorType.Noise:
                // Set frequency
                SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency, currentTime);
                break;
            case AudioOscillatorType.Pulse:
                // Set frequency
                SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency, currentTime);
                // Set pulsewidth
                PulseOscillator.SetPulseWidth(audioVoiceParameter.PulseWidth, currentTime);
                // Set Pulse Width ADSR
                PulseOscillator.SetPulseWidthDepthADSR(currentTime);
                break;
            default:
                break;
        }
    }

    private void SwitchOscillatorConnection(AudioOscillatorType newOscillatorType)
    {
        // If current oscillator is the same as the requested one, do nothing (assume it's already connected)
        if (newOscillatorType == CurrentOscillatorType)
            return;

        // If any other oscillator is currently connected
        if (CurrentOscillatorType != AudioOscillatorType.None)
            // Stop any existing playing audio will also disconnect it's oscillator
            Stop();

        // Then connect the specified oscillator
        ConnectOscillator(newOscillatorType);

        // Remember the new current oscillator
        CurrentOscillatorType = newOscillatorType;
    }

    internal void StartAudioADSPhase(AudioVoiceParameter audioVoiceParameter)
    {
        var currentTime = _audioContext.GetCurrentTime();

        if (_stopAndRecreateOscillator)
        {
            // 1. Stop current oscillator (if any) and release it's resoruces.
            // 2. Create new oscillator (even if same as before)
            //      With parameters such as Frequency, PulseWidth, etc.
            //      With Callback when ADSR envelope is finished to stop audio by stopping the oscillator (which then cannot be used anymore)
            // 3. Connect oscillator to gain node
            // 4. Set Gain ADSR envelope
            // 5. Start oscillator -> This will start the audio

            StopOscillatorNow(CurrentOscillatorType);
            CurrentOscillatorType = audioVoiceParameter.OscillatorType;
            CreateOscillator(audioVoiceParameter);
            ConnectOscillator(CurrentOscillatorType);
            SetGainADS(audioVoiceParameter, currentTime);
            StartOscillator(CurrentOscillatorType);
        }
        else
        {
            // Assume oscillator is already created and started
            // 1. Connect oscillator to gain node (and disconnect previous oscillator if different)
            // 2. Set parameters on existing oscillator such as Frequency, PulseWidth, etc.
            // 3. Set Gain ADSR envelope -> This will start the audio
            // 4. Set Callback to stop audio by setting Gain to 0 when envelope is finished

            SwitchOscillatorConnection(audioVoiceParameter.OscillatorType);
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

        if (_stopAndRecreateOscillator)
        {
            // Plan oscillator built-in delayed stop with callback
            StopOscillatorLater(CurrentOscillatorType, currentTime + audioVoiceParameter.ReleaseDurationSeconds);
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
        if (CurrentOscillatorType == AudioOscillatorType.Noise)
        {
            var playbackRate = NoiseOscillator.GetPlaybackRateFromFrequency(frequency);
            var playbackRateAudioParam = NoiseOscillator.NoiseGenerator!.GetPlaybackRate();

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
        switch (CurrentOscillatorType)
        {
            case AudioOscillatorType.None:
                return;
            case AudioOscillatorType.Triangle:
                frequencyAudioParam = TriangleOscillator!.TriangleOscillator!.GetFrequency();
                break;
            case AudioOscillatorType.Sawtooth:
                frequencyAudioParam = SawToothOscillator!.SawToothOscillator!.GetFrequency();
                break;
            case AudioOscillatorType.Pulse:
                frequencyAudioParam = PulseOscillator!.PulseOscillator!.GetFrequency();
                break;
            default:
                throw new NotImplementedException();
        }

        // Check if the frequency of the actual oscillator is different from the new frequency
        // TODO: Is this necessary to check? Could the frequency have been changed in other way?
        var currentFrequencyValue = frequencyAudioParam.GetCurrentValue();
        if (currentFrequencyValue != frequency)
        {
            AddDebugMessage($"Changing freq to {frequency}.");
            frequencyAudioParam.SetValueAtTime(frequency, changeTime);
        }
    }
}
