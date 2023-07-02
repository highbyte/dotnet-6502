using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMVoiceContext
    {
        private WASMSoundHandlerContext _soundHandlerContext;
        private Action<string, int> _addDebugMessage;

        private readonly byte _voice;
        public byte Voice => _voice;
        public SoundStatus Status = SoundStatus.Stopped;
        public GainNodeSync? GainNode;

        // SID Triangle or Sawtooth Oscillator
        public OscillatorNodeSync? Oscillator;

        // SID pulse oscillator
        public CustomPulseOscillatorNodeSync? PulseOscillator;
        public GainNodeSync? PulseWidthGainNode;
        public OscillatorNodeSync? LFOOscillator;

        // SID noise oscillator
        public AudioBufferSourceNodeSync? NoiseGenerator;


        public EventListener<EventSync> SoundStoppedCallback;

        private System.Threading.Timer _adsCycleCompleteTimer;
        private System.Threading.Timer _releaseCycleCompleteTimer;


        private readonly SemaphoreSlim _semaphoreSlim = new(1);
        public SemaphoreSlim SemaphoreSlim => _semaphoreSlim;

        public C64WASMVoiceContext(byte voice)
        {
            _voice = voice;
        }

        public void Init(WASMSoundHandlerContext soundHandlerContext, Action<string, int> addDebugMessage)
        {
            Oscillator = null;
            PulseOscillator = null;
            GainNode = null;
            PulseWidthGainNode = null;
            NoiseGenerator = null;
            Status = SoundStatus.Stopped;

            _soundHandlerContext = soundHandlerContext;
            _addDebugMessage = addDebugMessage;

            // Define callback handler to know when an oscillator has stopped playing.
            SoundStoppedCallback = EventListener<EventSync>.Create(_soundHandlerContext.AudioContext.WebAudioHelper, _soundHandlerContext.AudioContext.JSRuntime, (e) =>
            {
                Status = SoundStatus.Stopped;
                _addDebugMessage($"Oscillator Stop Callback triggered. Voice status is now: {Status}", Voice);
            });
        }

        public void ScheduleSoundStopAfterDecay(int waitMs)
        {
            // Set timer to stop sound after a while via a .NET timer
            _adsCycleCompleteTimer = new System.Threading.Timer((_) =>
            {
                Status = SoundStatus.Stopped;
                _addDebugMessage($"Scheduled Stop triggered. Voice status is now: {Status}", Voice);

            }, null, waitMs, Timeout.Infinite);
        }

        public void ScheduleSoundStopAfterRelease(int waitMs)
        {
            // Set timer to stop sound after a while via a .NET timer
            _releaseCycleCompleteTimer = new System.Threading.Timer((_) =>
            {
                Status = SoundStatus.Stopped;
            }, null, waitMs, Timeout.Infinite);
        }

        //public void Stop()
        //{
        //    if (Oscillator != null)
        //    {
        //        //Oscillator.Stop();
        //        Oscillator.Disconnect();
        //        Oscillator = null;
        //    }
        //    if (PulseOscillator != null)
        //    {
        //        //PulseOscillator.Stop();
        //        PulseOscillator.Disconnect();
        //        PulseOscillator = null;
        //    }
        //    if (LFOOscillator != null)
        //    {
        //        //LFOOscillator.Stop();
        //        LFOOscillator.Disconnect();
        //        LFOOscillator = null;
        //    }
        //    if (GainNode != null)
        //    {
        //        GainNode.Disconnect();
        //        GainNode = null;
        //    }
        //    if (PulseWidthGainNode != null)
        //    {
        //        PulseWidthGainNode.Disconnect();
        //        PulseWidthGainNode = null;
        //    }
        //    if (NoiseGenerator != null)
        //    {
        //        //NoiseGenerator.Stop();
        //        NoiseGenerator.Disconnect();
        //        NoiseGenerator = null;
        //    }

        //    Status = SoundStatus.Stopped;
        //}

        public void Stop()
        {
            _addDebugMessage($"Stop issued", Voice);

            if (GainNode != null)
            {
                _addDebugMessage($"Cancelling current GainNode schedule", Voice);
                var gainAudioParam = GainNode!.GetGain();
                var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();

                gainAudioParam.CancelScheduledValues(currentTime);
                gainAudioParam.SetValueAtTime(0, currentTime);
            }

            if (Status != SoundStatus.Stopped)
            {
                Status = SoundStatus.Stopped;
                _addDebugMessage($"Voice status is now: {Status}", Voice);
            }
            else
            {
                _addDebugMessage($"Voice status already was Stopped", Voice);
            }
        }
    }
}
