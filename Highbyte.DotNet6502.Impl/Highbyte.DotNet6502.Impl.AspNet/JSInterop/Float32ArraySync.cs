using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop;

public class Float32ArraySync : BaseJSWrapperSync
{
    public static Float32ArraySync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference)
    {
        return new Float32ArraySync(helper, jSRuntime, jSReference);
    }

    public Float32ArraySync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    public float GetLength()
    {
        return _helper.Invoke<float>("getAttribute", JSReference, "length");
    }

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
