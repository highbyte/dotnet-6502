// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

public class PeriodicWaveSync : BaseJSWrapperSync
{
    public static PeriodicWaveSync Create(IJSRuntime jSRuntime, BaseAudioContextSync context, PeriodicWaveOptions options)
    {
        var jSInstance = context.WebAudioHelper.Invoke<IJSInProcessObjectReference>("createPeriodicWave", context.JSReference, options);

        return new PeriodicWaveSync(context.WebAudioHelper, jSRuntime, jSInstance);
    }

    protected PeriodicWaveSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }
}
