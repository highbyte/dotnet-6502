// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

public class BaseAudioContextSync : EventTargetSync
{
    public IJSInProcessObjectReference WebAudioHelper => _helper;

    protected BaseAudioContextSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    public AudioDestinationNodeSync GetDestination()
    {
        //var helperVal = helper.Value;
        var helperVal = _helper;
        var jSIntance = helperVal.Invoke<IJSInProcessObjectReference>("getAttribute", JSReference, "destination");
        return AudioDestinationNodeSync.Create(_helper, JSRuntime, jSIntance);
    }

    public PeriodicWaveSync CreatePeriodicWave(float[] real, float[] imag, PeriodicWaveConstraints? constraints = null)
    {
        var options = new PeriodicWaveOptions
        {
            Real = real,
            Imag = imag,
        };
        if (constraints is not null)
            options.DisableNormalization = constraints.DisableNormalization;

        return PeriodicWaveSync.Create(JSRuntime, this, options);

    }
}
