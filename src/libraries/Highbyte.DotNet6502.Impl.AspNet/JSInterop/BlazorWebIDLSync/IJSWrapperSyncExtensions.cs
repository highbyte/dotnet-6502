// --------------------------------------------------------------------------------
// Synchronous Blazor WebIDL interop code based on KristofferStrube's Blazor.WebIDL library.
// https://github.com/KristofferStrube/Blazor.WebIDL
// --------------------------------------------------------------------------------

using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebIDLSync;

public interface IJSWrapperSync<T> : IJSWrapperSync where T : IJSWrapperSync<T>
{
    public static abstract T Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference);
}
