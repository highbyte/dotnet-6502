// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

public class PeriodicWaveConstraints
{
    [JsonPropertyName("disableNormalization")]
    public bool DisableNormalization { get; set; } = false;
}
