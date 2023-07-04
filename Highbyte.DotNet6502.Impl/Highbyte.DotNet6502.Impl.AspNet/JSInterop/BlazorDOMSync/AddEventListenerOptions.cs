// --------------------------------------------------------------------------------
// Synchronous Blazor DOM interop code based on KristofferStrube's Blazor.DOM library.
// https://github.com/KristofferStrube/Blazor.DOM
// --------------------------------------------------------------------------------
using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;

/// <summary>
/// <see href="https://dom.spec.whatwg.org/#dictdef-addeventlisteneroptions">AddEventListenerOptions browser specs</see>
/// </summary>
public class AddEventListenerOptions : EventListenerOptions
{
    [JsonPropertyName("passive")]
    public bool Passive { get; set; }

    [JsonPropertyName("once")]
    public bool Once { get; set; }
}
