using Highbyte.DotNet6502.Impl.NAudio.Synth;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using NAudio.Wave.SampleProviders;

namespace Highbyte.DotNet6502.Impl.NAudio.Commodore64.Audio
{
    public class C64NAudioVoiceContext
    {
        private readonly bool _disconnectOscillatorOnStop = true;

        private C64NAudioAudioHandler _audioHandler = default!;
        internal C64NAudioAudioHandler AudioHandler => _audioHandler;

        private Action<string, int?, SidVoiceWaveForm?, AudioVoiceStatus?> _addDebugMessage = default!;

        internal void AddDebugMessage(string msg)
        {
            _addDebugMessage(msg, _voice, CurrentSidVoiceWaveForm, Status);
        }

        private readonly byte _voice;
        public byte Voice => _voice;
        public AudioVoiceStatus Status = AudioVoiceStatus.Stopped;
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
        public SynthEnvelopeProvider TriangleOscillator { get; private set; } = default!;

        // SID Sawtooth Oscillator
        public SynthEnvelopeProvider SawToothOscillator { get; private set; } = default!;

        // SID pulse oscillator
        public SynthEnvelopeProvider PulseOscillator { get; private set; } = default!;

        // SID noise oscillator
        public SynthEnvelopeProvider NoiseOscillator { get; private set; } = default!;

        //private EventListener<EventSync> _audioStoppedCallback;

        private readonly Timer _adsCycleCompleteTimer = default!;
        private readonly Timer _releaseCycleCompleteTimer = default!;

        //private readonly SemaphoreSlim _semaphoreSlim = new(1);
        //public SemaphoreSlim SemaphoreSlim => _semaphoreSlim;

        public C64NAudioVoiceContext(byte voice)
        {
            _voice = voice;
        }

        internal void Init(
            C64NAudioAudioHandler audioHandler,
            Action<string, int?, SidVoiceWaveForm?, AudioVoiceStatus?> addDebugMessage)
        {
            Status = AudioVoiceStatus.Stopped;

            _audioHandler = audioHandler;
            _addDebugMessage = addDebugMessage;

            // Create oscillators in advance 
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
                _audioHandler.Mixer.AddMixerInput(oscillator);
        }

        private void DisconnectOscillator(SidVoiceWaveForm sidVoiceWaveForm)
        {
            AddDebugMessage($"Disconnecting oscillator: {sidVoiceWaveForm}");
            var oscillator = GetOscillator(sidVoiceWaveForm);
            if (oscillator != null)
                _audioHandler.Mixer.RemoveMixerInput(oscillator);
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
                    break;
                case SidVoiceWaveForm.Sawtooth:
                    SawToothOscillator = new SynthEnvelopeProvider(SignalGeneratorType.SawTooth);
                    break;
                case SidVoiceWaveForm.Pulse:
                    PulseOscillator = new SynthEnvelopeProvider(SignalGeneratorType.Square);
                    break;
                case SidVoiceWaveForm.RandomNoise:
                    NoiseOscillator = new SynthEnvelopeProvider(SignalGeneratorType.White);
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
            // Assume oscillator is already created and started
            // 1. Add oscillator to Mixer (and remove previous oscillator if different)
            // 2. Set parameters on existing oscillator such as Frequency, PulseWidth, etc.
            // 3. Set Gain ADSR envelope -> This will start the audio
            // 4. ? Set Callback to stop audio by setting Gain to 0 when envelope is finished

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

            StartAttackPhase(CurrentSidVoiceWaveForm);

            Status = AudioVoiceStatus.ADSCycleStarted;
            AddDebugMessage($"Status changed");
        }

        internal void StartAudioReleasePhase(AudioVoiceParameter audioVoiceParameter)
        {
            SetGainRelease(audioVoiceParameter);

            StopOscillatorLater(CurrentSidVoiceWaveForm);

            Status = AudioVoiceStatus.ReleaseCycleStarted;
            AddDebugMessage($"Status changed");
        }

        private void SetGainADS(AudioVoiceParameter audioVoiceParameter)
        {
            AddDebugMessage($"Setting Attack ({audioVoiceParameter.AttackDurationSeconds}) Decay ({audioVoiceParameter.DecayDurationSeconds}) Sustain ({audioVoiceParameter.SustainGain})");

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
                oscillator.ReleaseSeconds = (float)audioVoiceParameter.ReleaseDurationSeconds;
        }

        internal void SetFrequencyOnCurrentOscillator(float frequency)
        {
            AddDebugMessage($"Changing freq to {frequency}.");

            var oscillator = CurrentOscillator;
            if (oscillator != null)
                oscillator.Frequency = (double)frequency;
        }

        internal void SetPulseWidthOnCurrentOscillator(float pulseWidth)
        {
            AddDebugMessage($"Changing pulsewidth to {pulseWidth}.");

            var oscillator = CurrentOscillator;
            if (oscillator != null)
                oscillator.Duty = (double)pulseWidth;
        }
    }
}
