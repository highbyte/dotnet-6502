using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMPulseOscillator
    {
        private readonly C64WASMVoiceContext _c64WASMVoiceContext;
        private WASMSoundHandlerContext _soundHandlerContext => _c64WASMVoiceContext.SoundHandlerContext;
        private AudioContextSync _audioContext => _c64WASMVoiceContext.AudioContext;
        public byte _voice => _c64WASMVoiceContext.Voice;

        private Action<string> _addDebugMessage => _c64WASMVoiceContext.AddDebugMessage;

        // SID pulse oscillator
        public CustomPulseOscillatorNodeSync? PulseOscillator;
        public GainNodeSync? PulseWidthGainNode;
        public OscillatorNodeSync? LFOOscillator;

        public EventListener<EventSync> SoundStoppedCallback => _c64WASMVoiceContext.SoundStoppedCallback;

        public C64WASMPulseOscillator(C64WASMVoiceContext c64WASMVoiceContext)
        {
            _c64WASMVoiceContext = c64WASMVoiceContext;
        }

        public void Create(float frequency, float defaultPulseWidth)
        {
            // Create Pulse Oscillator
            PulseOscillator = CustomPulseOscillatorNodeSync.Create(
                _soundHandlerContext!.JSRuntime,
                _soundHandlerContext.AudioContext,
                new()
                {
                    Frequency = frequency,

                    //Pulse width - 1 to + 1 = ratio of the waveform's duty (power) cycle /mark-space
                    //DefaultWidth = -1.0   // 0% duty cycle  - silent
                    //DefaultWidth = -0.5f  // 25% duty cycle
                    //DefaultWidth = 0      // 50% duty cycle
                    //DefaultWidth = 0.5f   // 75% duty cycle
                    //DefaultWidth = 1.0f   // 100% duty cycle 
                    DefaultWidth = defaultPulseWidth
                });

            // Set callback on Pulse Oscillator (which is the primary oscillator in this case)
            PulseOscillator.AddEndedEventListsner(SoundStoppedCallback);


            // Create Pulse Width GainNode for pulse width modulation
            PulseWidthGainNode = GainNodeSync.Create(
                _soundHandlerContext!.JSRuntime,
                _soundHandlerContext.AudioContext,
                new()
                {
                    Gain = 0
                });
            PulseWidthGainNode.Connect(PulseOscillator.WidthGainNode);


            // Create low frequency oscillator, use as base for Pulse Oscillator.
            LFOOscillator = OscillatorNodeSync.Create(
                 _soundHandlerContext!.JSRuntime,
                 _soundHandlerContext.AudioContext,
                    new OscillatorOptions
                    {
                        Type = OscillatorType.Triangle,
                        Frequency = 10
                    });

            //LFOOscillator.Connect(detuneDepth);
            LFOOscillator.Connect(PulseWidthGainNode);

        }

        public void Start()
        {
            if (PulseOscillator == null)
                throw new Exception($"PulseOscillator is null. Call CreatePulseOscillator() first.");
            _addDebugMessage($"Starting PulseOscillator and LFOOscillator");
            PulseOscillator!.Start();
            LFOOscillator!.Start();
        }

        public void Stop()
        {
            if (PulseOscillator == null)
                throw new Exception($"PulseOscillator is null. Call Create() first.");
            _addDebugMessage($"Stopping and removing PulseOscillator, LFOOscillator, and related resources.");
            PulseOscillator!.Stop();
            LFOOscillator!.Stop();
            PulseWidthGainNode!.Disconnect();
            PulseOscillator = null;  // Make sure the oscillator is not reused. After .Stop() it isn't designed be used anymore.
            LFOOscillator = null;
            PulseWidthGainNode = null;
        }

        public void Connect()
        {
            if (PulseOscillator == null)
                throw new Exception($"PulseOscillator is null. Call Create() first.");
            PulseOscillator!.Connect(_c64WASMVoiceContext.GainNode!);
        }

        public void Disconnect()
        {
            if (PulseOscillator == null)
                throw new Exception($"PulseOscillator is null. Call Create() first.");
            PulseOscillator!.Disconnect();
        }

        internal void SetPulseWidthDepthADSR(double currentTime)
        {
            // Set Pulse Width ADSR (will start playing immediately if oscillator is already started)
            var widthDepthGainNodeAudioParam = PulseWidthGainNode!.GetGain();
            var oscWidthDepth = 0.5f;   // LFO depth - Pulse modulation depth (percent) // TODO: Configurable?
            var oscWidthAttack = 0.05f; // TODO: Configurable?
            //var oscWidthDecay = 0.4f;   // TODO: Configurable?
            var oscWidthSustain = 0.4f; // TODO: Configurable?
            var oscWidthRelease = 0.4f; // TODO: Configurable?
            var widthDepthSustainTime = currentTime + oscWidthAttack + oscWidthRelease;
            widthDepthGainNodeAudioParam.CancelScheduledValues(currentTime);
            widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0.5f * oscWidthDepth, currentTime + oscWidthAttack);
            widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0.5f * oscWidthDepth * oscWidthSustain, widthDepthSustainTime);
            widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0, oscWidthSustain + oscWidthRelease);

        }

        internal void SetPulseWidth(float pulseWidth, double changeTime)
        {
            var widthDepthGainNodeAudioParam = PulseWidthGainNode!.GetGain();

            // Check if the pulse width of the actual oscillator is different from the new frequency
            // TODO: Is this necessary to check? Could the pulse width have been changed in other way?
            var currentPulseWidthValue = widthDepthGainNodeAudioParam.GetCurrentValue();
            if (currentPulseWidthValue != pulseWidth)
            {
                _addDebugMessage($"Changing pulse width to {pulseWidth}.");
                widthDepthGainNodeAudioParam.SetValueAtTime(pulseWidth, changeTime);
            }
        }
    }
}
