using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

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

    public float[] GetCurve()
    {
        return WebAudioHelper.Invoke<float[]>("getAttribute", JSReference, "curve");
    }

    public void SetCurve(float[] curve)
    {
        WebAudioHelper.InvokeVoid("setAttribute", JSReference, new object[2] { "curve", curve });
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
