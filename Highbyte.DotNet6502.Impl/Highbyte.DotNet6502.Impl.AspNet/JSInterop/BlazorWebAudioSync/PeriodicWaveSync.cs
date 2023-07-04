using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

public class PeriodicWaveSync : BaseJSWrapperSync
{
    /// <summary>
    /// Example wave table values for real and imag parameters:
    /// https://github.com/GoogleChromeLabs/web-audio-samples/tree/main/src/demos/wavetable-synth/wave-tables
    /// </summary>
    /// <param name="jSRuntime"></param>
    /// <param name="context"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static PeriodicWaveSync Create(IJSRuntime jSRuntime, BaseAudioContextSync context, PeriodicWaveOptions options)
    {
        // Float arrays cannot be passed via JS interop, so we create a new anonymous options object with the float arrays converted to double arrays.
        var optionsInternal = new
        {
            real = options.Real.Select(c => (double)c).ToArray(),
            imag = options.Imag.Select(c => (double)c).ToArray(),
            disableNormalization = options.DisableNormalization
        };
        var jSInstance = context.WebAudioHelper.Invoke<IJSInProcessObjectReference>("constructPeriodicWave", context.JSReference, optionsInternal);

        return new PeriodicWaveSync(context.WebAudioHelper, jSRuntime, jSInstance);
    }

    protected PeriodicWaveSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }
}
