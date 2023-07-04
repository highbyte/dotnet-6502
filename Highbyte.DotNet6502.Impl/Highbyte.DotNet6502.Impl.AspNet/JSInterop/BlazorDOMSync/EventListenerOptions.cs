// --------------------------------------------------------------------------------
// Synchronous Blazor DOM interop code based on KristofferStrube's Blazor.DOM library.
// https://github.com/KristofferStrube/Blazor.DOM
// --------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;

/// <summary>
/// <see href="https://dom.spec.whatwg.org/#dictdef-eventlisteneroptions">EventListenerOptions browser specs</see>
/// </summary>
public class EventListenerOptions
{
    [JsonPropertyName("capture")]
    public bool Capture { get; set; }
}
