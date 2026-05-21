using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio;

public class WebAudioTriangleOscillator
{
    private readonly WebAudioVoiceContext _voiceContext;
    private WASMAudioHandlerContext _audioHandlerContext => _voiceContext.AudioHandlerContext;

    private Action<string> _addDebugMessage => _voiceContext.AddDebugMessage;

    // Triangle Oscillator
    internal OscillatorNodeSync? TriangleOscillator;

    public WebAudioTriangleOscillator(WebAudioVoiceContext voiceContext)
    {
        _voiceContext = voiceContext;
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
            throw new DotNet6502Exception($"TriangleOscillator is null. Call Create() first.");
        _addDebugMessage($"Planning stopp of TriangleOscillator: {when}");
        TriangleOscillator!.Stop(when);
    }

    internal void Start()
    {
        if (TriangleOscillator == null)
            throw new DotNet6502Exception($"TriangleOscillator is null. Call Create() first.");
        _addDebugMessage($"Starting TriangleOscillator");
        TriangleOscillator!.Start();
    }

    internal void Connect()
    {
        if (TriangleOscillator == null)
            throw new DotNet6502Exception($"TriangleOscillator is null. Call Create() first.");
        TriangleOscillator!.Connect(_voiceContext.GainNode!);
    }

    internal void Disconnect()
    {
        if (TriangleOscillator == null)
            return;
        TriangleOscillator!.Disconnect();
    }
}
