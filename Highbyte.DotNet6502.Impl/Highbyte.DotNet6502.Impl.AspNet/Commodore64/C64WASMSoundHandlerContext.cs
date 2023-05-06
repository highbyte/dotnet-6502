using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
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
        public SoundStatus Status = SoundStatus.Stopped;
        public GainNodeSync? GainNode;

        public OscillatorNodeSync? Oscillator;

        public CustomPulseOscillatorNodeSync? PulseOscillator;
        public GainNodeSync? PulseWidthGainNode;

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

            Status = SoundStatus.Stopped;
        }
    }
}
