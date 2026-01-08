// --------------------------------------------------------------------------------
// Synchronous Blazor WebIDL interop code based on KristofferStrube's Blazor.WebIDL library.
// https://github.com/KristofferStrube/Blazor.WebIDL
// --------------------------------------------------------------------------------

using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebIDLSync;

public interface IJSWrapperSync
{
    IJSInProcessObjectReference JSReference { get; }

    IJSRuntime JSRuntime { get; }
}
