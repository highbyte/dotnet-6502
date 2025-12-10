// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync.Options;

public class GainOptions
{
    [JsonPropertyName("gain")]
    public float Gain { get; set; } = 1;
}
