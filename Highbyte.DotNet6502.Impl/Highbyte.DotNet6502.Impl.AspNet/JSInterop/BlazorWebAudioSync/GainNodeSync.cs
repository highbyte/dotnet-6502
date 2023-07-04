// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

public class GainNodeSync : AudioNodeSync
{
    public static GainNodeSync Create(IJSRuntime jSRuntime, BaseAudioContextSync context, GainOptions? options = null)
    {
        var jSInstance = context.WebAudioHelper.Invoke<IJSInProcessObjectReference>("constructGainNode", context.JSReference, options);
        return new GainNodeSync(context.WebAudioHelper, jSRuntime, jSInstance);
    }

    protected GainNodeSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    public AudioParamSync GetGain()
    {
        var jSInstance = WebAudioHelper.Invoke<IJSInProcessObjectReference>("getAttribute", JSReference, "gain");
        return AudioParamSync.Create(_helper, JSRuntime, jSInstance);
    }
}
