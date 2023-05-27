using Highbyte.DotNet6502.Impl.AspNet.JSInterop;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Highbyte.DotNet6502.Systems;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMSoundHandlerContext : ISoundHandlerContext
    {
        private readonly AudioContextSync _audioContext;
        public AudioContextSync AudioContext => _audioContext;

        private readonly IJSRuntime _jsRuntime;
        public IJSRuntime JSRuntime => _jsRuntime;


        private AudioBufferSync _noiseBuffer;
        public AudioBufferSync NoiseBuffer => _noiseBuffer;

        public Dictionary<byte, C64WASMVoiceContext> VoiceContexts = new()
        {
            {1, new C64WASMVoiceContext(1) },
            {2, new C64WASMVoiceContext(2) },
            {3, new C64WASMVoiceContext(3) },
        };

        public C64WASMSoundHandlerContext(AudioContextSync audioContext, IJSRuntime jsRuntime)
        {
            _audioContext = audioContext;
            _jsRuntime = jsRuntime;
        }

        public void Init()
        {
            PrepareNoiseGenerator();

            foreach (var key in VoiceContexts.Keys)
            {
                var voice = VoiceContexts[key];
                voice.Init();
            }
        }

        private void PrepareNoiseGenerator()
        {
            float noiseDuration = 1.0f;  // Seconds

            var sampleRate = _audioContext.GetSampleRate();
            int bufferSize = (int)(sampleRate * noiseDuration);
            // Create an empty buffer
            _noiseBuffer = AudioBufferSync.Create(
                _audioContext.WebAudioHelper,
                _audioContext.JSRuntime,
                new AudioBufferOptions
                {
                    Length = bufferSize,
                    SampleRate = sampleRate,
                });

            // Note: Too slow to call Float32Array index in a loop
            //var data = noiseBuffer.GetChannelData(0);
            //var random = new Random();
            //for (var i = 0; i < bufferSize; i++)
            //{
            //    data[i] = ((float)random.NextDouble()) * 2 - 1;
            //}

            // Optimized by filling a .NET array, and then creating a Float32Array from that array in one call.
            float[] values = new float[bufferSize];
            var random = new Random();
            for (var i = 0; i < bufferSize; i++)
            {
                values[i] = ((float)random.NextDouble()) * 2 - 1;
            }
            var data = Float32ArraySync.Create(_audioContext.WebAudioHelper, _audioContext.JSRuntime, values);
            _noiseBuffer.CopyToChannel(data, 0);
        }
    }

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
