using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMVoiceContext
    {
        private readonly byte _voice;
        public byte Voice => _voice;
        public SoundStatus Status = SoundStatus.Stopped;
        public GainNodeSync? GainNode;

        // SID Triangle or Sawtooth Oscillator
        public OscillatorNodeSync? Oscillator;

        // SID pulse oscillator
        public CustomPulseOscillatorNodeSync? PulseOscillator;
        public GainNodeSync? PulseWidthGainNode;

        // SID noise oscillator
        public AudioBufferSourceNodeSync? NoiseGenerator;

        private readonly SemaphoreSlim _semaphoreSlim = new(1);
        public SemaphoreSlim SemaphoreSlim => _semaphoreSlim;

        public C64WASMVoiceContext(byte voice)
        {
            _voice = voice;
        }

        public void Init()
        {
            Oscillator = null;
            PulseOscillator = null;
            GainNode = null;
            PulseWidthGainNode = null;
            NoiseGenerator = null;
            Status = SoundStatus.Stopped;
        }

        public void Stop()
        {
            if (Oscillator != null)
            {
                Oscillator.Stop();
                Oscillator.Disconnect();
                Oscillator = null;
            }
            if (PulseOscillator != null)
            {
                PulseOscillator.Stop();
                PulseOscillator.Disconnect();
                PulseOscillator = null;
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
                NoiseGenerator.Stop();
                NoiseGenerator.Disconnect();
                NoiseGenerator = null;
            }

            Status = SoundStatus.Stopped;
        }
    }
}
