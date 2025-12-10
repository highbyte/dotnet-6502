using System.Threading.Channels;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;

public class WaveShaperNodeSync : AudioNodeSync
{
    public static WaveShaperNodeSync Create(IJSRuntime jSRuntime, BaseAudioContextSync context)
    {
        var jSInstance = context.WebAudioHelper.Invoke<IJSInProcessObjectReference>("constructWaveShaperNode", context.JSReference);
        return new WaveShaperNodeSync(context.WebAudioHelper, jSRuntime, jSInstance);
    }

    protected WaveShaperNodeSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    public Float32ArraySync GetCurve()
    {
        var jSIntance = WebAudioHelper.Invoke<IJSInProcessObjectReference>("getAttribute", JSReference, "curve");
        return Float32ArraySync.Create(WebAudioHelper, JSRuntime, jSIntance);
    }

    /// <summary>
    /// </summary>
    /// <param name="curve"></param>
    public void SetCurve(Float32ArraySync curve)
    {
        WebAudioHelper.InvokeVoid("setAttribute", JSReference, "curve", curve.JSReference);
    }

    // TODO: Oversample
    //public OversampleType GetOversample()
    //{
    //    var oversample = WebAudioHelper.Invoke<string>("getAttribute", JSReference, "oversample");
    //    return (OversampleType)Enum.Parse(typeof(OversampleType), oversample);
    //}
    //public void SetOversample(OversampleType oversample)
    //{
    //    WebAudioHelper.InvokeVoid("setAttribute", JSReference, "oversample", oversample);
    //}
}
