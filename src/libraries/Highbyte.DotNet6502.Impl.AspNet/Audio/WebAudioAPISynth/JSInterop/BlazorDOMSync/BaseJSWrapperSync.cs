// --------------------------------------------------------------------------------
// Synchronous Blazor DOM interop code based on KristofferStrube's Blazor.DOM library.
// https://github.com/KristofferStrube/Blazor.DOM
// --------------------------------------------------------------------------------
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebIDLSync;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorDOMSync;

public abstract class BaseJSWrapperSync : IJSWrapperSync, IAsyncDisposable, IDisposable
{
    protected readonly IJSInProcessObjectReference _helper;
    public IJSInProcessObjectReference JSReference { get; }

    public IJSRuntime JSRuntime { get; }

    internal BaseJSWrapperSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference)
    {
        _helper = helper;

        JSReference = jSReference;
        JSRuntime = jSRuntime;
    }

    public void Dispose()
    {
        _helper?.Dispose();

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_helper != null)
            await _helper.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
