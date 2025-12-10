// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorDOMSync;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;

public class AudioScheduledSourceNodeSync : AudioNodeSync
{
    protected AudioScheduledSourceNodeSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    /// <summary>
    /// Schedules a sound to playback at an exact time.
    /// </summary>
    /// <param name="when">The when parameter describes at what time (in seconds) the sound should start playing. It is in the same time coordinate system as the AudioContext's currentTime attribute.</param>
    public void Start(double when = 0)
    {
        JSReference.InvokeVoid("start", when);
    }

    /// <summary>
    /// Schedules a sound to playback at an exact time.
    /// </summary>
    /// <param name="when">The when parameter describes at what time (in seconds) the sound should start playing. It is in the same time coordinate system as the AudioContext's currentTime attribute.</param>
    /// <param name="offset">An offset, specified as the number of seconds in the same time coordinate system as the AudioContext, to the time within the audio buffer that playback should begin.</param>
    public void Start(double when, double offset)
    {
        JSReference.InvokeVoid("start", when, offset);
    }

    /// <summary>
    /// Schedules a sound to playback at an exact time.
    /// </summary>
    /// <param name="when">The when parameter describes at what time (in seconds) the sound should start playing. It is in the same time coordinate system as the AudioContext's currentTime attribute.</param>
    /// <param name="offset">An offset, specified as the number of seconds in the same time coordinate system as the AudioContext, to the time within the audio buffer that playback should begin.</param>
    /// <param name="duration">The duration of the sound to be played, specified in seconds.</param>
    public void Start(double when, double offset, double duration)
    {
        JSReference.InvokeVoid("start", when, offset, duration);
    }

    /// <summary>
    /// Schedules a sound to stop playback at an exact time.
    /// </summary>
    /// <param name="when">The when parameter describes at what time (in seconds) the source should stop playing. It is in the same time coordinate system as the AudioContext's currentTime attribute.</param>
    public void Stop(double when = 0)
    {
        JSReference.InvokeVoid("stop", when);
    }

    public void AddEndedEventListsner(EventListener<EventSync> callback)
    {
        AddEventListener("ended", callback);
    }
}
