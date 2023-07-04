using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMVoiceContext
    {
        // Set to true to stop and recreate oscillator before each sound. Set to false to reuse oscillator.
        // If true: for each sound played, the oscillator will be stopped, recreated, and started. This is the way WebAudio API is designed to work, but is very resource heavy if using the C#/.NET WebAudio wrapper classes, because new instances are created continuously.
        // If false: the oscillator is only created and started once. When sounds are stopped, the gain (volume) is set to 0.
        private readonly bool _stopAndRecreateOscillator = false;

        // This setting is only used if _stopAndRecreateOscillator is true.
        // If true: when a sound is stopped (and gain/volume is set to 0), the oscillator is also disconnected from the audio context. This may help sounds bleeding over when switching oscillator on same voice.
        // If false: when a sound is stopped (and gain/volume is set to 0), the oscillator stays connected to the audio context. This may increase performance, but may lead to sounds bleeding over when switching oscillators on same voice.
        private readonly bool _disconnectOscillatorOnStop = true;

        private WASMSoundHandlerContext _soundHandlerContext;
        internal WASMSoundHandlerContext SoundHandlerContext => _soundHandlerContext;
        private AudioContextSync _audioContext => _soundHandlerContext.AudioContext;

        private Action<string, int, SidVoiceWaveForm?, SoundStatus?> _addDebugMessage;

        internal void AddDebugMessage(string msg)
        {
            _addDebugMessage(msg, _voice, CurrentSidVoiceWaveForm, Status);
        }

        private readonly byte _voice;
        public byte Voice => _voice;
        public SoundStatus Status = SoundStatus.Stopped;
        public SidVoiceWaveForm CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;

        public GainNodeSync? GainNode;

        // SID Triangle Oscillator
        public C64WASMTriangleOscillator C64WASMTriangleOscillator { get; private set; }

        // SID Sawtooth Oscillator
        public C64WASMSawToothOscillator C64WASMSawToothOscillator { get; private set; }

        // SID pulse oscillator
        public C64WASMPulseOscillator C64WASMPulseOscillator { get; private set; }

        // SID noise oscillator
        public C64WASMNoiseOscillator C64WASMNoiseOscillator { get; private set; }

        private EventListener<EventSync> _soundStoppedCallback;

        private System.Threading.Timer _adsCycleCompleteTimer;
        private System.Threading.Timer _releaseCycleCompleteTimer;

        //private readonly SemaphoreSlim _semaphoreSlim = new(1);
        //public SemaphoreSlim SemaphoreSlim => _semaphoreSlim;

        public C64WASMVoiceContext(byte voice)
        {
            _voice = voice;
        }

        internal void Init(WASMSoundHandlerContext soundHandlerContext, Action<string, int, SidVoiceWaveForm?, SoundStatus?> addDebugMessage)
        {
            Status = SoundStatus.Stopped;

            _soundHandlerContext = soundHandlerContext;
            _addDebugMessage = addDebugMessage;

            // Create shared GainNode used as volume by all oscillators
            CreateGainNode();

            // Create implementations of the different oscillators
            C64WASMTriangleOscillator = new C64WASMTriangleOscillator(this);
            C64WASMSawToothOscillator = new C64WASMSawToothOscillator(this);
            C64WASMPulseOscillator = new C64WASMPulseOscillator(this);
            C64WASMNoiseOscillator = new C64WASMNoiseOscillator(this);

            if (_stopAndRecreateOscillator)
            {
                // Define callback handler to know when an oscillator has stopped playing. Only used if creating + starting oscillators before each sound.
                _soundStoppedCallback = EventListener<EventSync>.Create(_soundHandlerContext.AudioContext.WebAudioHelper, _soundHandlerContext.AudioContext.JSRuntime, (e) =>
                {
                    AddDebugMessage($"Oscillator Stop Callback triggered.");
                    Stop();
                });
            }
            else
            {
                // Unless we won't recreate/start the oscillator before each sound, create and start oscillators in advance 
                foreach (var sidWaveFormType in Enum.GetValues<SidVoiceWaveForm>())
                {
                    var wasmSoundParameters = new WASMVoiceParameter
                    {
                        SIDOscillatorType = sidWaveFormType,
                        Frequency = 300f,
                        PulseWidth = -0.22f,
                    };
                    CreateOscillator(wasmSoundParameters);
                    //ConnectOscillator(wasmSoundParameters.SIDOscillatorType);
                    StartOscillator(wasmSoundParameters.SIDOscillatorType);
                }
            }

            CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;
        }

        private void CreateGainNode()
        {
            GainNode = GainNodeSync.Create(_soundHandlerContext.JSRuntime, _soundHandlerContext.AudioContext);
            // Associate GainNode -> MasterVolume -> AudioContext destination 
            GainNode.Connect(_soundHandlerContext.MasterVolumeGainNode);
            var destination = _soundHandlerContext.AudioContext.GetDestination();
            _soundHandlerContext.MasterVolumeGainNode.Connect(destination);
        }

        private void ScheduleSoundStopAfterDecay(int waitMs)
        {
            // Set timer to stop sound after a while via a .NET timer
            _adsCycleCompleteTimer = new System.Threading.Timer((_) =>
            {
                AddDebugMessage($"Scheduled Stop after Decay triggered.");
                Stop();
            }, null, waitMs, Timeout.Infinite);
        }

        private void ScheduleSoundStopAfterRelease(double releaseDurationSeconds)
        {
            AddDebugMessage($"Scheduling voice stop at now + {releaseDurationSeconds} seconds.");

            // Schedule Stop for oscillator and other audio sources) when the Release period if over
            //voiceContext.Oscillator?.Stop(currentTime + wasmSoundParameters.ReleaseDurationSeconds);
            //voiceContext.PulseOscillator?.Stop(currentTime + wasmSoundParameters.ReleaseDurationSeconds);
            //voiceContext.NoiseGenerator?.Stop(currentTime + wasmSoundParameters.ReleaseDurationSeconds);

            var waitMs = (int)(releaseDurationSeconds * 1000.0d);
            // Set timer to stop sound after a while via a .NET timer
            _releaseCycleCompleteTimer = new System.Threading.Timer((_) =>
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
                if (Status != SoundStatus.Stopped)
                {
                    StopOscillatorNow(CurrentSidVoiceWaveForm);
                }
                CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;
            }
            else
            {
                // In this scenario, the oscillator is still running. Set volume to 0 in the GainNode to ensure no sound is playing. 
                AddDebugMessage($"Cancelling current GainNode schedule");
                var gainAudioParam = GainNode!.GetGain();
                var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();
                gainAudioParam.CancelScheduledValues(currentTime);
                gainAudioParam.SetValueAtTime(0, currentTime);

                // If configured, disconnect the oscillator when stopping
                if (_disconnectOscillatorOnStop)
                {
                    DisconnectOscillator(CurrentSidVoiceWaveForm);
                    CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;
                }
            }

            if (Status != SoundStatus.Stopped)
            {
                Status = SoundStatus.Stopped;
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

        private void CreateOscillator(WASMVoiceParameter wasmSoundParameters)
        {
            AddDebugMessage($"Creating oscillator: {wasmSoundParameters.SIDOscillatorType}");

            switch (wasmSoundParameters.SIDOscillatorType)
            {
                case SidVoiceWaveForm.None:
                    break;
                case SidVoiceWaveForm.Triangle:
                    C64WASMTriangleOscillator?.Create(wasmSoundParameters.Frequency);
                    if (_stopAndRecreateOscillator)
                        C64WASMTriangleOscillator!.TriangleOscillator!.AddEndedEventListsner(_soundStoppedCallback);
                    break;
                case SidVoiceWaveForm.Sawtooth:
                    C64WASMSawToothOscillator?.Create(wasmSoundParameters.Frequency);
                    if (_stopAndRecreateOscillator)
                        C64WASMSawToothOscillator!.SawToothOscillator!.AddEndedEventListsner(_soundStoppedCallback);
                    break;
                case SidVoiceWaveForm.Pulse:
                    C64WASMPulseOscillator?.Create(wasmSoundParameters.Frequency, wasmSoundParameters.PulseWidth);
                    if (_stopAndRecreateOscillator)
                        C64WASMPulseOscillator!.PulseOscillator!.AddEndedEventListsner(_soundStoppedCallback);
                    break;
                case SidVoiceWaveForm.RandomNoise:
                    var playbackRate = C64WASMNoiseOscillator.GetPlaybackRateFromFrequency(wasmSoundParameters.Frequency);
                    C64WASMNoiseOscillator?.Create(playbackRate);
                    if (_stopAndRecreateOscillator)
                        C64WASMNoiseOscillator!.NoiseGenerator!.AddEndedEventListsner(_soundStoppedCallback);
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

        private void SetOscillatorParameters(WASMVoiceParameter wasmSoundParameters, double currentTime)
        {
            AddDebugMessage($"Setting oscillator parameters: {wasmSoundParameters.SIDOscillatorType}");

            switch (wasmSoundParameters.SIDOscillatorType)
            {
                case SidVoiceWaveForm.None:
                    // Set frequency
                    SetFrequencyOnCurrentOscillator(wasmSoundParameters.Frequency, currentTime);
                    break;
                case SidVoiceWaveForm.Triangle:
                    // Set frequency 
                    SetFrequencyOnCurrentOscillator(wasmSoundParameters.Frequency, currentTime);
                    break;
                case SidVoiceWaveForm.Sawtooth:
                    // Set frequency
                    SetFrequencyOnCurrentOscillator(wasmSoundParameters.Frequency, currentTime);
                    break;
                case SidVoiceWaveForm.Pulse:
                    // Set frequency 
                    SetFrequencyOnCurrentOscillator(wasmSoundParameters.Frequency, currentTime);
                    // Set pulsewidth
                    C64WASMPulseOscillator.SetPulseWidth(wasmSoundParameters.PulseWidth, currentTime);
                    // Set Pulse Width ADSR
                    C64WASMPulseOscillator.SetPulseWidthDepthADSR(currentTime);
                    break;
                case SidVoiceWaveForm.RandomNoise:
                    // Set frequency (playback rate) on current NoiseGenerator
                    SetFrequencyOnCurrentOscillator(wasmSoundParameters.Frequency, currentTime);
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
            {
                // Stop any existing playing sound will also disconnect it's oscillator
                Stop();
            }

            // Then connect the specified oscillator
            ConnectOscillator(newSidVoiceWaveForm);

            // Remember the new current oscillator
            CurrentSidVoiceWaveForm = newSidVoiceWaveForm;
        }

        internal void StartSoundADSPhase(WASMVoiceParameter wasmSoundParameters)
        {
            var currentTime = _audioContext.GetCurrentTime();

            if (_stopAndRecreateOscillator)
            {
                // 1. Stop current oscillator (if any) and release it's resoruces.
                // 2. Create new oscillator (even if same as before)
                //      With parameters such as Frequency, PulseWidth, etc.
                //      With Callback when ADSR envelope is finished to stop sound by stopping the oscillator (which then cannot be used anymore)
                // 3. Connect oscillator to gain node
                // 4. Set Gain ADSR envelope
                // 5. Start oscillator -> This will start the sound

                StopOscillatorNow(CurrentSidVoiceWaveForm);
                CurrentSidVoiceWaveForm = wasmSoundParameters.SIDOscillatorType;
                CreateOscillator(wasmSoundParameters);
                ConnectOscillator(CurrentSidVoiceWaveForm);
                SetGainADS(wasmSoundParameters, currentTime);
                StartOscillator(CurrentSidVoiceWaveForm);
            }
            else
            {
                // Assume oscillator is already created and started
                // 1. Connect oscillator to gain node (and disconnect previous oscillator if different)
                // 2. Set parameters on existing oscillator such as Frequency, PulseWidth, etc.
                // 3. Set Gain ADSR envelope -> This will start the sound
                // 4. Set Callback to stop sound by setting Gain to 0 when envelope is finished

                SwitchOscillatorConnection(wasmSoundParameters.SIDOscillatorType);
                SetOscillatorParameters(wasmSoundParameters, currentTime);
                SetGainADS(wasmSoundParameters, currentTime);

                // If SustainGain is 0, then we need to schedule a stop of the sound
                // when the attack + decay period is over.
                if (wasmSoundParameters.SustainGain == 0)
                {
                    var waitSeconds = wasmSoundParameters.AttackDurationSeconds + wasmSoundParameters.DecayDurationSeconds;
                    AddDebugMessage($"Scheduling voice stop now + {waitSeconds} seconds.");
                    ScheduleSoundStopAfterDecay(waitMs: (int)(waitSeconds * 1000.0d));
                }
            }

            Status = SoundStatus.ADSCycleStarted;
            AddDebugMessage($"Status changed");
        }

        internal void StartSoundReleasePhase(WASMVoiceParameter wasmSoundParameters)
        {
            var currentTime = _audioContext.GetCurrentTime();
            SetGainRelease(wasmSoundParameters, currentTime);

            if (_stopAndRecreateOscillator)
            {
                // Plan oscillator built-in delayed stop with callback
                StopOscillatorLater(CurrentSidVoiceWaveForm, currentTime + wasmSoundParameters.ReleaseDurationSeconds);
            }
            else
            {
                // Plan manual callback after release duration (as we don't stop the oscillator in this scenario, as it cannot be started again)
                ScheduleSoundStopAfterRelease(wasmSoundParameters.ReleaseDurationSeconds);
            }

            Status = SoundStatus.ReleaseCycleStarted;
            AddDebugMessage($"Status changed");
        }

        private void SetGainADS(WASMVoiceParameter wasmSoundParameters, double currentTime)
        {
            AddDebugMessage($"Setting Gain ({wasmSoundParameters.Gain}) Attack ({wasmSoundParameters.AttackDurationSeconds}) Decay ({wasmSoundParameters.DecayDurationSeconds}) Sustain ({wasmSoundParameters.SustainGain})");

            // Set Attack/Decay/Sustain gain envelope
            var gainAudioParam = GainNode!.GetGain();
            gainAudioParam.CancelScheduledValues(currentTime);
            gainAudioParam.SetValueAtTime(0, currentTime);
            gainAudioParam.LinearRampToValueAtTime(wasmSoundParameters.Gain, currentTime + wasmSoundParameters.AttackDurationSeconds);
            gainAudioParam.LinearRampToValueAtTime(wasmSoundParameters.SustainGain, currentTime + wasmSoundParameters.AttackDurationSeconds + wasmSoundParameters.DecayDurationSeconds);
            //gainAudioParam.SetTargetAtTime(wasmSoundParameters.SustainGain, currentTime + wasmSoundParameters.AttackDurationSeconds, wasmSoundParameters.DecayDurationSeconds);
        }

        private void SetGainRelease(WASMVoiceParameter wasmSoundParameters, double currentTime)
        {
            AddDebugMessage($"Setting Gain Release ({wasmSoundParameters.ReleaseDurationSeconds})");

            // Schedule a volume change from current gain level down to 0 during specified Release time 
            var gainAudioParam = GainNode!.GetGain();
            var currentGainValue = gainAudioParam.GetCurrentValue();
            gainAudioParam.CancelScheduledValues(currentTime);
            gainAudioParam.SetValueAtTime(currentGainValue, currentTime);
            gainAudioParam.LinearRampToValueAtTime(0, currentTime + wasmSoundParameters.ReleaseDurationSeconds);
        }

        /// <summary>
        /// Set volume of the GainNode used by all oscillators
        /// </summary>
        /// <param name="gain"></param>
        /// <param name="changeTime"></param>
        internal void SetVolume(float gain, double changeTime)
        {
            // The current time is where the gain change starts
            var gainAudioParam = GainNode!.GetGain();
            // Check if the gain of the actual oscillator is different from the new gain
            // (the gain could have changed by ADSR cycle, LinearRampToValueAtTimeAsync)
            var currentGainValue = gainAudioParam.GetCurrentValue();
            if (currentGainValue != gain)
            {
                AddDebugMessage($"Changing vol to {gain}.");
                gainAudioParam.SetValueAtTime(gain, changeTime);
            }
        }

        internal void SetFrequencyOnCurrentOscillator(float frequency, double changeTime)
        {
            // Noise sample generator
            if (CurrentSidVoiceWaveForm == SidVoiceWaveForm.RandomNoise)
            {
                var playbackRate = C64WASMNoiseOscillator.GetPlaybackRateFromFrequency(frequency);
                AudioParamSync playbackRateAudioParam = C64WASMNoiseOscillator.NoiseGenerator!.GetPlaybackRate();

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
                //var gainAudioParam = GainNode!.GetGain();
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
}
