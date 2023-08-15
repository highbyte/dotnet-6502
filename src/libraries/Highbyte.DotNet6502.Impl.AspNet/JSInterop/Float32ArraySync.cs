using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop;

public class Float32ArraySync : BaseJSWrapperSync
{
    public static Float32ArraySync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference)
    {
        return new Float32ArraySync(helper, jSRuntime, jSReference);
    }

    public static Float32ArraySync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime)
    {
        var jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructFloat32Array");
        return new Float32ArraySync(helper, jSRuntime, jSInstance);
    }

    public static Float32ArraySync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, float[] values)
    {
        // Convert float array to double array because Blazor JS Interop does not support double arrays.
        var valuesDouble = values.Select(c => (double)c).ToArray();
        var jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructFloat32Array", valuesDouble);
        return new Float32ArraySync(helper, jSRuntime, jSInstance);
    }

    public Float32ArraySync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    public float GetLength()
    {
        return _helper.Invoke<float>("getAttribute", JSReference, "length");
    }

    /// <summary>
    /// Access the actual values inside the JS Float32Array.
    /// Note: Is very slow if accessing many items. Instead create a Float32Array from a .NET float array (see Create method overload above).
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public float this[int index]
    {
        get
        {
            return _helper.Invoke<float>("getArrayValue", JSReference, index);
        }
        set
        {
            _helper.Invoke<float>("setArrayValue", JSReference, index, value);
        }
    }
}
