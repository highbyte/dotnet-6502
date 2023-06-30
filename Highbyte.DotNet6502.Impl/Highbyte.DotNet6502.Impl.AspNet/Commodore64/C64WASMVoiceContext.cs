using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMVoiceContext
    {
        private WASMSoundHandlerContext _soundHandlerContext;
        private Action<string> _addDebugMessage;

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

        private readonly SemaphoreSlim _semaphoreSlim = new(1);
        public SemaphoreSlim SemaphoreSlim => _semaphoreSlim;

        public C64WASMVoiceContext(byte voice)
        {
            _voice = voice;
        }

        public void Init(WASMSoundHandlerContext soundHandlerContext, Action<string> addDebugMessage)
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
                _addDebugMessage($"Sound stopped on voice {Voice}.");
                Status = SoundStatus.Stopped;
            });
        }

        public void Stop()
        {
            if (Oscillator != null)
            {
                //Oscillator.Stop();
                Oscillator.Disconnect();
                Oscillator = null;
            }
            if (PulseOscillator != null)
            {
                //PulseOscillator.Stop();
                PulseOscillator.Disconnect();
                PulseOscillator = null;
            }
            if (LFOOscillator != null)
            {
                //LFOOscillator.Stop();
                LFOOscillator.Disconnect();
                LFOOscillator = null;
            }
            if (GainNode != null)
            {
                GainNode.Disconnect();
                GainNode = null;
            }
            if (PulseWidthGainNode != null)
            {
                PulseWidthGainNode.Disconnect();
                PulseWidthGainNode = null;
            }
            if (NoiseGenerator != null)
            {
                //NoiseGenerator.Stop();
                NoiseGenerator.Disconnect();
                NoiseGenerator = null;
            }

            Status = SoundStatus.Stopped;
        }

        public void Pause()
        {
            if (Oscillator != null)
            {
                //Oscillator.Stop();
                //Oscillator.Disconnect();
                //Oscillator = null;
            }
            if (PulseOscillator != null)
            {
                //PulseOscillator.Stop();
                //PulseOscillator.Disconnect();
                //PulseOscillator = null;
            }
            if (LFOOscillator != null)
            {
                //LFOOscillator.Stop();
                //LFOOscillator.Disconnect();
                //LFOOscillator = null;
            }

            if (GainNode != null)
            {
                //GainNode.Disconnect();
                //GainNode = null;
            }
            if (PulseWidthGainNode != null)
            {
                //PulseWidthGainNode.Disconnect();
                //PulseWidthGainNode = null;
            }
            if (NoiseGenerator != null)
            {
                //NoiseGenerator.Stop();
                //NoiseGenerator.Disconnect();
                //NoiseGenerator = null;
            }

            Status = SoundStatus.Stopped;
        }
    }
}
