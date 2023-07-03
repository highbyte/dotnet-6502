using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMSawToothOscillator
    {
        private readonly C64WASMVoiceContext _c64WASMVoiceContext;
        private WASMSoundHandlerContext _soundHandlerContext => _c64WASMVoiceContext.SoundHandlerContext;
        private AudioContextSync _audioContext => _c64WASMVoiceContext.AudioContext;
        public byte _voice => _c64WASMVoiceContext.Voice;

        private Action<string> _addDebugMessage => _c64WASMVoiceContext.AddDebugMessage;

        // SID SawTooth Oscillator
        public OscillatorNodeSync? SawToothOscillator;

        public EventListener<EventSync> SoundStoppedCallback => _c64WASMVoiceContext.SoundStoppedCallback;

        public C64WASMSawToothOscillator(C64WASMVoiceContext c64WASMVoiceContext)
        {
            _c64WASMVoiceContext = c64WASMVoiceContext;
        }

        public void Create(float frequency)
        {
            // Create SawTooth Oscillator
            SawToothOscillator = OscillatorNodeSync.Create(
                _soundHandlerContext!.JSRuntime,
                _soundHandlerContext.AudioContext,
                new()
                {
                    Type = OscillatorType.Sawtooth,
                    Frequency = frequency,
                });

            // Set callback on Oscillator
            SawToothOscillator.AddEndedEventListsner(SoundStoppedCallback);
        }

        public void Start()
        {
            if (SawToothOscillator == null)
                throw new Exception($"SawToothOscillator is null. Call Create() first.");
            _addDebugMessage($"Starting SawToothOscillator");
            SawToothOscillator!.Start();
        }

        public void Stop()
        {
            if (SawToothOscillator == null)
                throw new Exception($"SawToothOscillator is null. Call Create() first.");
            _addDebugMessage($"Stopping and removing SawToothOscillator");
            SawToothOscillator!.Stop();
            SawToothOscillator = null;  // Make sure the oscillator is not reused. After .Stop() it isn't designed be used anymore.
        }

        public void Connect()
        {
            if (SawToothOscillator == null)
                throw new Exception($"SawToothOscillator is null. Call Create() first.");
            SawToothOscillator!.Connect(_c64WASMVoiceContext.GainNode!);
        }

        public void Disconnect()
        {
            if (SawToothOscillator == null)
                throw new Exception($"SawToothOscillator is null. Call Create() first.");
            SawToothOscillator!.Disconnect();
        }
    }
}
