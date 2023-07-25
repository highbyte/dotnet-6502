using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMTriangleOscillator
    {
        private readonly C64WASMVoiceContext _c64WASMVoiceContext;
        private WASMAudioHandlerContext _audioHandlerContext => _c64WASMVoiceContext.AudioHandlerContext;

        private Action<string> _addDebugMessage => _c64WASMVoiceContext.AddDebugMessage;

        // SID Triangle Oscillator
        internal OscillatorNodeSync? TriangleOscillator;

        public C64WASMTriangleOscillator(C64WASMVoiceContext c64WASMVoiceContext)
        {
            _c64WASMVoiceContext = c64WASMVoiceContext;
        }

        internal void Create(float frequency)
        {
            // Create Triangle Oscillator
            TriangleOscillator = OscillatorNodeSync.Create(
                _audioHandlerContext!.JSRuntime,
                _audioHandlerContext.AudioContext,
                new()
                {
                    Type = OscillatorType.Triangle,
                    Frequency = frequency,
                });
        }

        internal void StopNow()
        {
            if (TriangleOscillator == null)
                return;
            TriangleOscillator!.Stop();
            TriangleOscillator = null;  // Make sure the oscillator is not reused. After .Stop() it isn't designed be used anymore.
            _addDebugMessage($"Stopped and removed TriangleOscillator");
        }

        internal void StopLater(double when)
        {
            if (TriangleOscillator == null)
                throw new Exception($"TriangleOscillator is null. Call Create() first.");
            _addDebugMessage($"Planning stopp of TriangleOscillator: {when}");
            TriangleOscillator!.Stop(when);
        }

        internal void Start()
        {
            if (TriangleOscillator == null)
                throw new Exception($"TriangleOscillator is null. Call Create() first.");
            _addDebugMessage($"Starting TriangleOscillator");
            TriangleOscillator!.Start();
        }

        internal void Connect()
        {
            if (TriangleOscillator == null)
                throw new Exception($"TriangleOscillator is null. Call Create() first.");
            TriangleOscillator!.Connect(_c64WASMVoiceContext.GainNode!);
        }

        internal void Disconnect()
        {
            if (TriangleOscillator == null)
                return;
            TriangleOscillator!.Disconnect();
        }
    }
}
