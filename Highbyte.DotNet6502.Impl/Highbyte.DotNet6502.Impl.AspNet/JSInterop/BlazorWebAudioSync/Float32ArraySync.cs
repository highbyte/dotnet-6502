using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

public class Float32ArraySync : BaseJSWrapperSync
{
    public static Float32ArraySync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, int length)
    {
        double[] valuesInternal = new double[length];
        var jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructFloat32Array", valuesInternal);
        return new Float32ArraySync(helper, jSRuntime, jSInstance);
    }

    public static Float32ArraySync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, float[] values)
    {
        var valuesInternal = values.Select(c => (double)c).ToArray();
        var jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructFloat32Array", valuesInternal);
        return new Float32ArraySync(helper, jSRuntime, jSInstance);
    }

    public Float32ArraySync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }
}
