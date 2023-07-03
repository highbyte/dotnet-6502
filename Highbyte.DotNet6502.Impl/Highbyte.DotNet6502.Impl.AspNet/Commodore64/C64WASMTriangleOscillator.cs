using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMTriangleOscillator
    {
        private readonly C64WASMVoiceContext _c64WASMVoiceContext;
        private WASMSoundHandlerContext _soundHandlerContext => _c64WASMVoiceContext.SoundHandlerContext;
        private AudioContextSync _audioContext => _c64WASMVoiceContext.AudioContext;
        public byte _voice => _c64WASMVoiceContext.Voice;

        private Action<string> _addDebugMessage => _c64WASMVoiceContext.AddDebugMessage;

        // SID Triangle Oscillator
        public OscillatorNodeSync? TriangleOscillator;

        public EventListener<EventSync> SoundStoppedCallback => _c64WASMVoiceContext.SoundStoppedCallback;

        public C64WASMTriangleOscillator(C64WASMVoiceContext c64WASMVoiceContext)
        {
            _c64WASMVoiceContext = c64WASMVoiceContext;
        }

        public void Create(float frequency)
        {
            // Create Triangle Oscillator
            TriangleOscillator = OscillatorNodeSync.Create(
                _soundHandlerContext!.JSRuntime,
                _soundHandlerContext.AudioContext,
                new()
                {
                    Type = OscillatorType.Triangle,
                    Frequency = frequency,
                });

            // Set callback on Oscillator
            TriangleOscillator.AddEndedEventListsner(SoundStoppedCallback);
        }

        public void Stop()
        {
            if (TriangleOscillator == null)
                throw new Exception($"TriangleOscillator is null. Call Create() first.");
            _addDebugMessage($"Stopping and removing TriangleOscillator");
            TriangleOscillator!.Stop();
            TriangleOscillator = null;  // Make sure the oscillator is not reused. After .Stop() it isn't designed be used anymore.
        }

        public void Start()
        {
            if (TriangleOscillator == null)
                throw new Exception($"TriangleOscillator is null. Call Create() first.");
            _addDebugMessage($"Starting TriangleOscillator");
            TriangleOscillator!.Start();
        }

        public void Connect()
        {
            if (TriangleOscillator == null)
                throw new Exception($"TriangleOscillator is null. Call Create() first.");
            TriangleOscillator!.Connect(_c64WASMVoiceContext.GainNode!);
        }

        public void Disconnect()
        {
            if (TriangleOscillator == null)
                throw new Exception($"TriangleOscillator is null. Call Create() first.");
            TriangleOscillator!.Disconnect();
        }

    }
}
