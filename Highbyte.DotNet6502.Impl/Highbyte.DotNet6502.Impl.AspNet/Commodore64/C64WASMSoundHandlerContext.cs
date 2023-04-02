using Highbyte.DotNet6502.Systems;
using KristofferStrube.Blazor.WebAudio;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMSoundHandlerContext : ISoundHandlerContext
    {
        private readonly AudioContext _audioContext;
        public AudioContext AudioContext => _audioContext;

        private readonly IJSRuntime _jsRuntime;
        public IJSRuntime JSRuntime => _jsRuntime;

        public Dictionary<byte, C64WASMVoiceContext> VoiceContexts = new()
        {
            {1, new C64WASMVoiceContext(1) },
            {2, new C64WASMVoiceContext(2) },
            {3, new C64WASMVoiceContext(3) },
        };

        public C64WASMSoundHandlerContext(AudioContext audioContext, IJSRuntime jsRuntime)
        {
            _audioContext = audioContext;
            _jsRuntime = jsRuntime;
        }

        public void Init()
        {
            foreach (var key in VoiceContexts.Keys)
            {
                var voice = VoiceContexts[key];
                voice.Init();
            }
        }
    }

    public class C64WASMVoiceContext
    {
        private readonly byte _voice;
        public byte Voice => _voice;
        public C64SoundStatus Status = C64SoundStatus.Stopped;
        public OscillatorNode? Oscillator;
        public GainNode? GainNode;

        private readonly SemaphoreSlim _semaphoreSlim = new(1);
        public SemaphoreSlim SemaphoreSlim => _semaphoreSlim;

        public float? CurrentGain { get; set; }
        public float? CurrentFrequency { get; set; }

        public C64WASMVoiceContext(byte voice)
        {
            _voice = voice;
        }

        public void Init()
        {
            Status = C64SoundStatus.Stopped;
            Oscillator = null;
            GainNode = null;
            CurrentGain = null;
            CurrentFrequency = null;
        }
    }
}
