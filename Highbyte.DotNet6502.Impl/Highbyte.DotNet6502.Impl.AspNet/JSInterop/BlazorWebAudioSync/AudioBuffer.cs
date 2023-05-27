using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

public class AudioBufferSync : BaseJSWrapperSync
{
    public static AudioBufferSync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, AudioBufferOptions? options = null)
    {
        var jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructAudioBuffer", options);
        return new AudioBufferSync(helper, jSRuntime, jSInstance);
    }

    public AudioBufferSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    //public float[] GetChannelData(int channel)
    //{
    //    return _helper.Invoke<float[]>("callMethodReturnFloat32Array", JSReference, "getChannelData", channel);
    //}

    public Float32ArraySync GetChannelData(int channel)
    {
        var jSIntance = _helper.Invoke<IJSInProcessObjectReference>("callMethod", JSReference, "getChannelData", channel);
        return Float32ArraySync.Create(_helper, JSRuntime, jSIntance);
    }

    //public void CopyToChannel(Float32Array source, int channel, int startInChannel)
    //{
    //    _helper.Invoke<IJSInProcessObjectReference>("callMethod", JSReference, "copyToChannel", source, channel, startInChannel);
    //}
}
