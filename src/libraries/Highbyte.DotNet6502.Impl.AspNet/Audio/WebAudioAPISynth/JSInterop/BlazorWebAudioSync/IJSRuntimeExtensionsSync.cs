// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;

internal static class IJSRuntimeExtensionsSync
{
    /// <summary>
    /// Imports a JS file that contains all the helper functions needed for the synchronous Blazor WebAudio interop code.
    /// Must be called before any other synchronous Blazor WebAudio interop code is called.
    /// 
    /// Importing the JS file in a synchronous call won't work.
    /// </summary>
    /// <param name="jSRuntime"></param>
    /// <returns></returns>
    internal static async Task<IJSInProcessObjectReference> GetInProcessHelperAllAsync(this IJSRuntime jSRuntime)
    {
        return await jSRuntime.InvokeAsync<IJSInProcessObjectReference>(
            "import", "./_content/Highbyte.DotNet6502.Impl.AspNet/JSInterop.Blazor.All.js");
    }
}
