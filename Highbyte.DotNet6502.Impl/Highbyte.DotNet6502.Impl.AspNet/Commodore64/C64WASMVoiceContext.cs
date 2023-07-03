using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMVoiceContext
    {
        private readonly bool _disconnectOscillatorOnStop = false;

        private WASMSoundHandlerContext _soundHandlerContext;
        public WASMSoundHandlerContext SoundHandlerContext => _soundHandlerContext;
        public AudioContextSync AudioContext => _soundHandlerContext.AudioContext;

        public Action<string, int, SidVoiceWaveForm?, SoundStatus?> _addDebugMessage;
        public void AddDebugMessage(string msg)
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


        public EventListener<EventSync> SoundStoppedCallback;

        private System.Threading.Timer _adsCycleCompleteTimer;
        private System.Threading.Timer _releaseCycleCompleteTimer;


        private readonly SemaphoreSlim _semaphoreSlim = new(1);
        public SemaphoreSlim SemaphoreSlim => _semaphoreSlim;

        public C64WASMVoiceContext(byte voice)
        {
            _voice = voice;
        }

        public void Init(WASMSoundHandlerContext soundHandlerContext, Action<string, int, SidVoiceWaveForm?, SoundStatus?> addDebugMessage, bool createAndStartOscillators)
        {
            Status = SoundStatus.Stopped;

            _soundHandlerContext = soundHandlerContext;
            _addDebugMessage = addDebugMessage;

            // Define callback handler to know when an oscillator has stopped playing. Only used if creating + starting oscillators before each sound.
            SoundStoppedCallback = EventListener<EventSync>.Create(_soundHandlerContext.AudioContext.WebAudioHelper, _soundHandlerContext.AudioContext.JSRuntime, (e) =>
            {
                AddDebugMessage($"Oscillator Stop Callback triggered.");
                Stop();
            });

            // Create shared GainNode used as volume by all oscillators
            CreateGainNode();

            // Create implementations of the different oscillators
            C64WASMTriangleOscillator = new C64WASMTriangleOscillator(this);
            C64WASMSawToothOscillator = new C64WASMSawToothOscillator(this);
            C64WASMPulseOscillator = new C64WASMPulseOscillator(this);
            C64WASMNoiseOscillator = new C64WASMNoiseOscillator(this);

            // Create and start oscillators in advance if requested
            if (createAndStartOscillators)
            {
                C64WASMTriangleOscillator.Create(frequency: 300f);
                C64WASMTriangleOscillator.Start();

                C64WASMSawToothOscillator.Create(frequency: 300f);
                C64WASMSawToothOscillator.Start();

                C64WASMPulseOscillator.Create(frequency: 110f, defaultPulseWidth: -0.22f);
                C64WASMPulseOscillator.Start();

                C64WASMNoiseOscillator.Create();
                C64WASMNoiseOscillator.Start();
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

        public void ScheduleSoundStopAfterDecay(int waitMs)
        {
            // Set timer to stop sound after a while via a .NET timer
            _adsCycleCompleteTimer = new System.Threading.Timer((_) =>
            {
                AddDebugMessage($"Scheduled Stop after Decay triggered.");
                Stop();
            }, null, waitMs, Timeout.Infinite);
        }

        public void ScheduleSoundStopAfterRelease(double releaseDurationSeconds)
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

        public void Stop()
        {
            AddDebugMessage($"Stop issued");

            AddDebugMessage($"Cancelling current GainNode schedule");
            var gainAudioParam = GainNode!.GetGain();
            var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();

            gainAudioParam.CancelScheduledValues(currentTime);
            gainAudioParam.SetValueAtTime(0, currentTime);

            if (Status != SoundStatus.Stopped)
            {
                Status = SoundStatus.Stopped;
                AddDebugMessage($"Status changed.");
            }
            else
            {
                AddDebugMessage($"Status already was Stopped");
            }


            // If configured, disconnect the oscillator when stopping
            if (_disconnectOscillatorOnStop)
            {
                DisconnectOscillator(CurrentSidVoiceWaveForm);
                CurrentSidVoiceWaveForm = SidVoiceWaveForm.None;
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

        internal void ConnectOscillator(SidVoiceWaveForm sidVoiceWaveForm)
        {
            // If current oscillator is the same as the requested one, do nothing (assume it's already connected)
            if (sidVoiceWaveForm == CurrentSidVoiceWaveForm)
                return;

            // If any other oscillator is currently connected
            if (CurrentSidVoiceWaveForm != SidVoiceWaveForm.None)
            {
                // Stop any existing playing sound will also disconnect it's oscillator
                Stop();
            }

            // Then connect the specified oscillator
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

            // Remember the new current oscillator
            CurrentSidVoiceWaveForm = sidVoiceWaveForm;
        }

        internal void SetGainADS(WASMVoiceParameter wasmSoundParameters, double currentTime)
        {
            AddDebugMessage($"Starting Gain ({wasmSoundParameters.Gain}) Attack ({wasmSoundParameters.AttackDurationSeconds}) Decay ({wasmSoundParameters.DecayDurationSeconds}) Sustain ({wasmSoundParameters.SustainGain})");

            // Set Attack/Decay/Sustain gain envelope
            var gainAudioParam = GainNode!.GetGain();
            gainAudioParam.CancelScheduledValues(currentTime);
            gainAudioParam.SetValueAtTime(0, currentTime);
            gainAudioParam.LinearRampToValueAtTime(wasmSoundParameters.Gain, currentTime + wasmSoundParameters.AttackDurationSeconds);
            gainAudioParam.SetTargetAtTime(wasmSoundParameters.SustainGain, currentTime + wasmSoundParameters.AttackDurationSeconds, wasmSoundParameters.DecayDurationSeconds);
        }

        internal void SetGainRelease(WASMVoiceParameter wasmSoundParameters, double currentTime)
        {
            AddDebugMessage($"Starting Gain ({wasmSoundParameters.Gain}) Attack ({wasmSoundParameters.AttackDurationSeconds}) Decay ({wasmSoundParameters.DecayDurationSeconds}) Sustain ({wasmSoundParameters.SustainGain})");

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
                AudioParamSync playbackRateAudioParam = C64WASMNoiseOscillator.NoiseGenerator!.GetPlaybackRate();

                // Hack: Set noise buffer sample playback rate to simulate change in noise frequency in SID.
                var playbackRate = GetPlaybackRateFromFrequency(frequency);

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

        internal float GetPlaybackRateFromFrequency(float frequency)
        {
            const float playbackRateMin = 0.0f; // Should be used for the minimum SID frequency ( 0 Hz)
            const float playbackRateMax = 1.0f; // Should be used for the maximum SID frequency ( ca 4000 Hz)
            const float sidFreqMin = 0;
            const float sidFreqMax = 4000;
            float playbackRate = playbackRateMin + (float)(frequency - sidFreqMin) / (sidFreqMax - sidFreqMin) * (playbackRateMax - playbackRateMin);
            return playbackRate;
        }
    }
}
