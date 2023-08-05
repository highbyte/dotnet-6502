using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Audio
{
    public class C64WASMSawToothOscillator
    {
        private readonly C64WASMVoiceContext _c64WASMVoiceContext;
        private WASMAudioHandlerContext _audioHandlerContext => _c64WASMVoiceContext.AudioHandlerContext;

        private Action<string> _addDebugMessage => _c64WASMVoiceContext.AddDebugMessage;

        // SID SawTooth Oscillator
        internal OscillatorNodeSync? SawToothOscillator;

        public C64WASMSawToothOscillator(C64WASMVoiceContext c64WASMVoiceContext)
        {
            _c64WASMVoiceContext = c64WASMVoiceContext;
        }

        internal void Create(float frequency)
        {
            // Create SawTooth Oscillator
            SawToothOscillator = OscillatorNodeSync.Create(
                _audioHandlerContext!.JSRuntime,
                _audioHandlerContext.AudioContext,
                new()
                {
                    Type = OscillatorType.Sawtooth,
                    Frequency = frequency,
                });
        }

        internal void Start()
        {
            if (SawToothOscillator == null)
                throw new Exception($"SawToothOscillator is null. Call Create() first.");
            _addDebugMessage($"Starting SawToothOscillator");
            SawToothOscillator!.Start();
        }

        internal void StopNow()
        {
            if (SawToothOscillator == null)
                return;
            SawToothOscillator!.Stop();
            SawToothOscillator = null;  // Make sure the oscillator is not reused. After .Stop() it isn't designed be used anymore.
            _addDebugMessage($"Stopped and removed SawToothOscillator");
        }

        internal void StopLater(double when)
        {
            if (SawToothOscillator == null)
                throw new Exception($"SawToothOscillator is null. Call Create() first.");
            _addDebugMessage($"Planning stopp of SawToothOscillator: {when}");
            SawToothOscillator!.Stop(when);
        }

        internal void Connect()
        {
            if (SawToothOscillator == null)
                throw new Exception($"SawToothOscillator is null. Call Create() first.");
            SawToothOscillator!.Connect(_c64WASMVoiceContext.GainNode!);
        }

        internal void Disconnect()
        {
            if (SawToothOscillator == null)
                throw new Exception($"SawToothOscillator is null. Call Create() first.");
            SawToothOscillator!.Disconnect();
        }
    }
}
