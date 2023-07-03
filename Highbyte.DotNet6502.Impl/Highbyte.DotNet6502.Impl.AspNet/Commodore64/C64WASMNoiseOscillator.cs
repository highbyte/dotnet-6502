using Highbyte.DotNet6502.Impl.AspNet.JSInterop;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMNoiseOscillator
    {
        private readonly C64WASMVoiceContext _c64WASMVoiceContext;
        private WASMSoundHandlerContext _soundHandlerContext => _c64WASMVoiceContext.SoundHandlerContext;
        private AudioContextSync _audioContext => _c64WASMVoiceContext.AudioContext;
        public byte _voice => _c64WASMVoiceContext.Voice;

        private Action<string> _addDebugMessage => _c64WASMVoiceContext.AddDebugMessage;

        // SID noise oscillator
        private AudioBufferSync _noiseBuffer;
        public AudioBufferSourceNodeSync? NoiseGenerator;

        public EventListener<EventSync> SoundStoppedCallback => _c64WASMVoiceContext.SoundStoppedCallback;

        public C64WASMNoiseOscillator(C64WASMVoiceContext c64WASMVoiceContext)
        {
            _c64WASMVoiceContext = c64WASMVoiceContext;
        }

        public void Create(float playbackRate = 0.2f)
        {
            if (_noiseBuffer == null)
                PrepareNoiseGenerator();
            _addDebugMessage($"Creating NoiseGenerator");
            NoiseGenerator = AudioBufferSourceNodeSync.Create(
                _soundHandlerContext.JSRuntime,
                _soundHandlerContext.AudioContext,
                new AudioBufferSourceNodeOptions
                {
                    PlaybackRate = playbackRate,    // Factor of sample rate. 1.0 = same speed as original.
                    Loop = true,
                    Buffer = _noiseBuffer
                });

            NoiseGenerator.AddEndedEventListsner(SoundStoppedCallback);
        }
        public void Start()
        {
            if (NoiseGenerator == null)
                throw new Exception($"NoiseGenerator is null. Call Create() first.");
            _addDebugMessage($"Starting NoiseGenerator");
            NoiseGenerator!.Start();
            //voiceContext!.NoiseGenerator.Start(0, 0, currentTime + wasmSoundParameters.AttackDurationSeconds + wasmSoundParameters.ReleaseDurationSeconds);
        }

        public void Stop()
        {
            if (NoiseGenerator == null)
                throw new Exception($"NoiseGenerator is null. Call Create() first.");
            _addDebugMessage($"Stopping and removing NoiseGenerator");
            NoiseGenerator!.Stop();
            NoiseGenerator = null;  // Make sure the NoiseGenerator is not reused. After .Stop() it isn't designed be used anymore.
        }

        public void Connect()
        {
            if (NoiseGenerator == null)
                throw new Exception($"NoiseGenerator is null. Call Create() first.");
            NoiseGenerator!.Connect(_c64WASMVoiceContext.GainNode!);
        }

        public void Disconnect()
        {
            if (NoiseGenerator == null)
                throw new Exception($"NoiseGenerator is null. Call Create() first.");
            NoiseGenerator!.Disconnect();
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
}
