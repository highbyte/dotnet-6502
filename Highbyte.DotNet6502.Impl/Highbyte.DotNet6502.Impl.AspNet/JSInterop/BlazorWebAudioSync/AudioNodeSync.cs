// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

public class AudioNodeSync : EventTargetSync
{
    protected IJSInProcessObjectReference WebAudioHelper => _helper;

    public static AudioNodeSync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference)
    {
        return new AudioNodeSync(helper, jSRuntime, jSReference);
    }

    protected AudioNodeSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    public AudioNodeSync Connect(AudioNodeSync destinationNode, ulong output = 0, ulong input = 0)
    {
        var jSInstance = JSReference.Invoke<IJSInProcessObjectReference>("connect", destinationNode.JSReference, output, input);
        return Create(WebAudioHelper, JSRuntime, jSInstance);
    }

    public void Disconnect()
    {
        JSReference.InvokeVoid("disconnect");
    }
}
