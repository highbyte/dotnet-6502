using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio;

public class WebAudioSawToothOscillator
{
    private readonly WebAudioVoiceContext _voiceContext;
    private WASMAudioHandlerContext _audioHandlerContext => _voiceContext.AudioHandlerContext;

    private Action<string> _addDebugMessage => _voiceContext.AddDebugMessage;

    // SawTooth Oscillator
    internal OscillatorNodeSync? SawToothOscillator;

    public WebAudioSawToothOscillator(WebAudioVoiceContext voiceContext)
    {
        _voiceContext = voiceContext;
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
            throw new DotNet6502Exception($"SawToothOscillator is null. Call Create() first.");
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
            throw new DotNet6502Exception($"SawToothOscillator is null. Call Create() first.");
        _addDebugMessage($"Planning stopp of SawToothOscillator: {when}");
        SawToothOscillator!.Stop(when);
    }

    internal void Connect()
    {
        if (SawToothOscillator == null)
            throw new DotNet6502Exception($"SawToothOscillator is null. Call Create() first.");
        SawToothOscillator!.Connect(_voiceContext.GainNode!);
    }

    internal void Disconnect()
    {
        if (SawToothOscillator == null)
            return;
        SawToothOscillator!.Disconnect();
    }
}
