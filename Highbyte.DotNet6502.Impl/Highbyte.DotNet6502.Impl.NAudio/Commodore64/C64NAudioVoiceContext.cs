using Highbyte.DotNet6502.Impl.NAudio.Synth;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using NAudio.Wave.SampleProviders;

namespace Highbyte.DotNet6502.Impl.NAudio.Commodore64
{
    public class C64NAudioVoiceContext
    {
        // Set to true to stop and recreate oscillator before each audio. Set to false to reuse oscillator.
        // If true: for each audio played, the oscillator will be stopped, recreated, and started. This is the way WebAudio API is designed to work, but is very resource heavy if using the C#/.NET WebAudio wrapper classes, because new instances are created continuously.
        // If false: the oscillator is only created and started once. When audio is stopped, the gain (volume) is set to 0.
        private readonly bool _stopAndRecreateOscillator = false;

        // This setting is only used if _stopAndRecreateOscillator is true.
        // If true: when audio is stopped (and gain/volume is set to 0), the oscillator is also disconnected from the audio context. This may help audio bleeding over when switching oscillator on same voice.
        // If false: when audio is stopped (and gain/volume is set to 0), the oscillator stays connected to the audio context. This may increase performance, but may lead to audio bleeding over when switching oscillators on same voice.
        private readonly bool _disconnectOscillatorOnStop = true;

        private C64NAudioAudioHandler _audioHandler;
        internal C64NAudioAudioHandler AudioHandler => _audioHandler;

        private Action<string, int, SidVoiceWaveForm?, AudioStatus?> _addDebugMessage;

        internal void AddDebugMessage(string msg)
        {
            _addDebugMessage(msg, _voice, CurrentSidVoiceWaveForm, Status);
        }

        private readonly byte _voice;
        public byte Voice => _voice;
        public AudioStatus Status = AudioStatus.Stopped;
        public SidVoiceWaveForm CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;


        public SynthEnvelopeProvider? GetOscillator(SidVoiceWaveForm sidVoiceWaveForm) => sidVoiceWaveForm switch
        {
            SidVoiceWaveForm.None => null,
            SidVoiceWaveForm.Triangle => TriangleOscillator,
            SidVoiceWaveForm.Sawtooth => SawToothOscillator,
            SidVoiceWaveForm.Pulse => PulseOscillator,
            SidVoiceWaveForm.RandomNoise => NoiseOscillator,
            _ => null
        };
        public SynthEnvelopeProvider? CurrentOscillator => GetOscillator(CurrentSidVoiceWaveForm);

        // SID Triangle Oscillator
        public SynthEnvelopeProvider TriangleOscillator { get; private set; }

        // SID Sawtooth Oscillator
        public SynthEnvelopeProvider SawToothOscillator { get; private set; }

        // SID pulse oscillator
        public SynthEnvelopeProvider PulseOscillator { get; private set; }

        // SID noise oscillator
        public SynthEnvelopeProvider NoiseOscillator { get; private set; }

        //private EventListener<EventSync> _audioStoppedCallback;

        private Timer _adsCycleCompleteTimer;
        private Timer _releaseCycleCompleteTimer;

        //private readonly SemaphoreSlim _semaphoreSlim = new(1);
        //public SemaphoreSlim SemaphoreSlim => _semaphoreSlim;

        public C64NAudioVoiceContext(byte voice)
        {
            _voice = voice;
        }

        internal void Init(
            C64NAudioAudioHandler audioHandler,
            Action<string, int, SidVoiceWaveForm?, AudioStatus?> addDebugMessage)
        {
            Status = AudioStatus.Stopped;

            _audioHandler = audioHandler;
            _addDebugMessage = addDebugMessage;

            if (_stopAndRecreateOscillator)
            {
                // TODO?
                //// Define callback handler to know when an oscillator has stopped playing. Only used if creating + starting oscillators before each audio.
                //_audioStoppedCallback = EventListener<EventSync>.Create(_audioHandlerContext.AudioContext.WebAudioHelper, _audioHandlerContext.AudioContext.JSRuntime, (e) =>
                //{
                //    AddDebugMessage($"Oscillator StopWavePlayer Callback triggered.");
                //    StopWavePlayer();
                //});
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
                }
            }

            CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;
        }

        //private void ScheduleAudioStopAfterDecay(int waitMs)
        //{
        //    // Set timer to stop audio after a while via a .NET timer
        //    _adsCycleCompleteTimer = new Timer((_) =>
        //    {
        //        AddDebugMessage($"Scheduled StopWavePlayer after Decay triggered.");
        //        StopWavePlayer();
        //    }, null, waitMs, Timeout.Infinite);
        //}

        //private void ScheduleAudioStopAfterRelease(double releaseDurationSeconds)
        //{
        //    AddDebugMessage($"Scheduling voice stop at now + {releaseDurationSeconds} seconds.");

        //    // Schedule StopWavePlayer for oscillator and other audio sources) when the Release period if over
        //    //voiceContext.Oscillator?.StopWavePlayer(currentTime + audioVoiceParameter.ReleaseDurationSeconds);
        //    //voiceContext.PulseOscillator?.StopWavePlayer(currentTime + audioVoiceParameter.ReleaseDurationSeconds);
        //    //voiceContext.NoiseGenerator?.StopWavePlayer(currentTime + audioVoiceParameter.ReleaseDurationSeconds);

        //    var waitMs = (int)(releaseDurationSeconds * 1000.0d);
        //    // Set timer to stop audio after a while via a .NET timer
        //    _releaseCycleCompleteTimer = new Timer((_) =>
        //    {
        //        AddDebugMessage($"Scheduled StopWavePlayer after Release triggered.");
        //        StopWavePlayer();
        //    }, null, waitMs, Timeout.Infinite);
        //}

        internal void Stop()
        {
            AddDebugMessage($"StopWavePlayer issued");

            if (_stopAndRecreateOscillator)
            {
                // This is called either via callback when oscillator sent "ended" event, or manually stopped via turning off SID gate.
                if (Status != AudioStatus.Stopped)
                    StopOscillatorNow(CurrentSidVoiceWaveForm);
                CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;
            }
            else
            {
                // In this scenario, the oscillator is still running. Set volume to 0 
                AddDebugMessage($"Mute oscillator");

                // Set ADSR state to idle
                ResetOscillatorADSR(CurrentSidVoiceWaveForm);

                // If configured, disconnect the oscillator when stopping
                if (_disconnectOscillatorOnStop)
                {
                    DisconnectOscillator(CurrentSidVoiceWaveForm);
                    CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;
                }
            }

            if (Status != AudioStatus.Stopped)
            {
                Status = AudioStatus.Stopped;
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
            var oscillator = GetOscillator(sidVoiceWaveForm);
            if (oscillator != null)
            {
                ResetOscillatorADSR(sidVoiceWaveForm);
                DisconnectOscillator(sidVoiceWaveForm);
            }
        }

        private void StopOscillatorLater(SidVoiceWaveForm sidVoiceWaveForm)
        {
            AddDebugMessage($"Stopping oscillator: {sidVoiceWaveForm} later");
            var oscillator = GetOscillator(sidVoiceWaveForm);
            oscillator?.StartRelease();
        }

        private void ConnectOscillator(SidVoiceWaveForm sidVoiceWaveForm)
        {
            AddDebugMessage($"Connecting oscillator: {sidVoiceWaveForm}");

            var oscillator = GetOscillator(sidVoiceWaveForm);
            if (oscillator != null)
            {
                _audioHandler.Mixer.AddMixerInput(oscillator);
            }
        }

        private void DisconnectOscillator(SidVoiceWaveForm sidVoiceWaveForm)
        {
            AddDebugMessage($"Disconnecting oscillator: {sidVoiceWaveForm}");
            var oscillator = GetOscillator(sidVoiceWaveForm);
            if (oscillator != null)
            {
                _audioHandler.Mixer.RemoveMixerInput(oscillator);
            }
        }

        private void ResetOscillatorADSR(SidVoiceWaveForm sidVoiceWaveForm)
        {
            AddDebugMessage($"Reseting oscillator ADSR: {sidVoiceWaveForm}");
            var oscillator = GetOscillator(sidVoiceWaveForm);
            oscillator?.ResetADSR();
        }

        private void CreateOscillator(AudioVoiceParameter audioVoiceParameter)
        {
            AddDebugMessage($"Creating oscillator: {audioVoiceParameter.SIDOscillatorType}");

            switch (audioVoiceParameter.SIDOscillatorType)
            {
                case SidVoiceWaveForm.None:
                    break;
                case SidVoiceWaveForm.Triangle:
                    TriangleOscillator = new SynthEnvelopeProvider(SignalGeneratorType.Triangle);
                    //if (_stopAndRecreateOscillator)
                    //    C64WASMTriangleOscillator!.TriangleOscillator!.AddEndedEventListsner(_audioStoppedCallback);
                    break;
                case SidVoiceWaveForm.Sawtooth:
                    SawToothOscillator = new SynthEnvelopeProvider(SignalGeneratorType.SawTooth);
                    //if (_stopAndRecreateOscillator)
                    //    C64WASMSawToothOscillator!.SawToothOscillator!.AddEndedEventListsner(_audioStoppedCallback);
                    break;
                case SidVoiceWaveForm.Pulse:
                    PulseOscillator = new SynthEnvelopeProvider(SignalGeneratorType.Square);
                    //if (_stopAndRecreateOscillator)
                    //    C64WASMPulseOscillator!.PulseOscillator!.AddEndedEventListsner(_audioStoppedCallback);
                    break;
                case SidVoiceWaveForm.RandomNoise:
                    NoiseOscillator = new SynthEnvelopeProvider(SignalGeneratorType.White);
                    //if (_stopAndRecreateOscillator)
                    //    C64WASMNoiseOscillator!.NoiseGenerator!.AddEndedEventListsner(_audioStoppedCallback);
                    break;
                default:
                    break;
            }
        }

        private void StartAttackPhase(SidVoiceWaveForm sidVoiceWaveForm)
        {
            AddDebugMessage($"Starting oscillator: {sidVoiceWaveForm}");

            var oscillator = GetOscillator(sidVoiceWaveForm);
            oscillator?.StartAttack();
        }

        private void SetOscillatorParameters(AudioVoiceParameter audioVoiceParameter)
        {
            AddDebugMessage($"Setting oscillator parameters: {audioVoiceParameter.SIDOscillatorType}");

            switch (audioVoiceParameter.SIDOscillatorType)
            {
                case SidVoiceWaveForm.None:
                    // Set frequency
                    SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency);
                    break;
                case SidVoiceWaveForm.Triangle:
                    // Set frequency 
                    SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency);
                    break;
                case SidVoiceWaveForm.Sawtooth:
                    // Set frequency
                    SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency);
                    break;
                case SidVoiceWaveForm.Pulse:
                    // Set frequency 
                    SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency);
                    // Set pulsewidth
                    SetPulseWidthOnCurrentOscillator(audioVoiceParameter.PulseWidth);
                    break;
                case SidVoiceWaveForm.RandomNoise:
                    // Set frequency (playback rate) on current NoiseGenerator
                    SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency);
                    break;
                default:
                    break;
            }
        }

        private void SwitchOscillatorConnection(SidVoiceWaveForm newSidVoiceWaveForm, bool forceSwitch = true)
        {
            // If current oscillator is the same as the requested one, do nothing (assume it's already connected)
            if (!forceSwitch && newSidVoiceWaveForm == CurrentSidVoiceWaveForm)
                return;

            // If any other oscillator is currently connected
            if (CurrentSidVoiceWaveForm != SidVoiceWaveForm.None)
                // StopWavePlayer any existing playing audio will also disconnect it's oscillator
                Stop();

            // Then connect the specified oscillator
            ConnectOscillator(newSidVoiceWaveForm);

            // Remember the new current oscillator
            CurrentSidVoiceWaveForm = newSidVoiceWaveForm;
        }

        internal void StartAudioADSPhase(AudioVoiceParameter audioVoiceParameter)
        {
            if (_stopAndRecreateOscillator)
            {
                // 1. StopWavePlayer current oscillator (if any) and release it's resoruces.
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
                SetGainADS(audioVoiceParameter);
            }
            else
            {
                // Assume oscillator is already created and started
                // 1. Connect oscillator to gain node (and disconnect previous oscillator if different)
                // 2. Set parameters on existing oscillator such as Frequency, PulseWidth, etc.
                // 3. Set Gain ADSR envelope -> This will start the audio
                // 4. Set Callback to stop audio by setting Gain to 0 when envelope is finished

                SwitchOscillatorConnection(audioVoiceParameter.SIDOscillatorType);
                SetOscillatorParameters(audioVoiceParameter);
                SetGainADS(audioVoiceParameter);

                // If SustainGain is 0, then we need to schedule a stop of the audio
                // when the attack + decay period is over.
                if (audioVoiceParameter.SustainGain == 0)
                {
                    //var waitSeconds = audioVoiceParameter.AttackDurationSeconds + audioVoiceParameter.DecayDurationSeconds;
                    //AddDebugMessage($"Scheduling voice stop now + {waitSeconds} seconds.");
                    //ScheduleAudioStopAfterDecay(waitMs: (int)(waitSeconds * 1000.0d));
                }
            }

            StartAttackPhase(CurrentSidVoiceWaveForm);

            Status = AudioStatus.ADSCycleStarted;
            AddDebugMessage($"Status changed");
        }

        internal void StartAudioReleasePhase(AudioVoiceParameter audioVoiceParameter)
        {
            SetGainRelease(audioVoiceParameter);

            //if (_stopAndRecreateOscillator)
                // Plan oscillator built-in delayed stop with callback
                StopOscillatorLater(CurrentSidVoiceWaveForm);
            //else
            //{
                // Plan manual callback after release duration (as we don't stop the oscillator in this scenario, as it cannot be started again)
                //ScheduleAudioStopAfterRelease(audioVoiceParameter.ReleaseDurationSeconds);
            //}

            Status = AudioStatus.ReleaseCycleStarted;
            AddDebugMessage($"Status changed");
        }

        private void SetGainADS(AudioVoiceParameter audioVoiceParameter)
        {
            AddDebugMessage($"Setting Gain ({audioVoiceParameter.Gain}) Attack ({audioVoiceParameter.AttackDurationSeconds}) Decay ({audioVoiceParameter.DecayDurationSeconds}) Sustain ({audioVoiceParameter.SustainGain})");

            // TODO: Set Attack/Decay/Sustain values
            var oscillator = CurrentOscillator;
            if (oscillator != null)
            {
                oscillator.AttackSeconds = (float)audioVoiceParameter.AttackDurationSeconds;
                oscillator.DecaySeconds = (float)audioVoiceParameter.DecayDurationSeconds;
                oscillator.SustainLevel = (float)audioVoiceParameter.SustainGain;
            }
        }

        private void SetGainRelease(AudioVoiceParameter audioVoiceParameter)
        {
            AddDebugMessage($"Setting Gain Release ({audioVoiceParameter.ReleaseDurationSeconds})");

            var oscillator = CurrentOscillator;
            if (oscillator != null)
            {
                oscillator.ReleaseSeconds = (float)audioVoiceParameter.ReleaseDurationSeconds;
            }

        }

        internal void SetFrequencyOnCurrentOscillator(float frequency)
        {
            AddDebugMessage($"Changing freq to {frequency}.");

            var oscillator = CurrentOscillator;
            if (oscillator != null)
            {
                oscillator.Frequency = (double)frequency;
            }
        }

        internal void SetPulseWidthOnCurrentOscillator(float pulseWidth)
        {
            AddDebugMessage($"Changing pulsewidth to {pulseWidth}.");

            var oscillator = CurrentOscillator;
            if (oscillator != null)
            {
                oscillator.Duty = (double)pulseWidth;
            }
        }
    }
}
