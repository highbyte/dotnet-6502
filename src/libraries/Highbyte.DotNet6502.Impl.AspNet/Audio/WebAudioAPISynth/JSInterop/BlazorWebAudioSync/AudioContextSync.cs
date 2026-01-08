// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;

public class AudioContextSync : BaseAudioContextSync
{
    public static AudioContextSync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, object? contextOptions = null)
    {
        var jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructAudioContext", contextOptions);
        return new AudioContextSync(helper, jSRuntime, jSInstance);
    }

    public static async Task<AudioContextSync> CreateAsync(IJSRuntime jSRuntime, object? contextOptions = null)
    {
        var helper = await jSRuntime.GetInProcessHelperAllAsync();
        var jSInstance = await helper.InvokeAsync<IJSInProcessObjectReference>("constructAudioContext", contextOptions);
        return new AudioContextSync(helper, jSRuntime, jSInstance);
    }

    protected AudioContextSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    public double GetCurrentTime()
    {
        var helper = WebAudioHelper;
        return helper.Invoke<double>("getAttribute", JSReference, "currentTime");
    }
}
