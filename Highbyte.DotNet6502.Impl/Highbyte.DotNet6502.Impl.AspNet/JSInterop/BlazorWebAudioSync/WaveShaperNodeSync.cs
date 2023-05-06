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
        return WebAudioHelper.Invoke<float[]>("getAttributeFloat32Array", JSReference, "curve");
    }

    /// <summary>
    /// </summary>
    /// <param name="curve"></param>
    public void SetCurve(float[] curve)
    {
        // Curve should be a float array, but Blazor JS interop only handles double[].
        var curveDouble = curve.Select(c => (double)c).ToArray();
        // Also, we must call a special JS function to converts the array to Float32Array in JS.
        WebAudioHelper.InvokeVoid("setAttributeFloat32Array", JSReference, "curve", curveDouble);
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
