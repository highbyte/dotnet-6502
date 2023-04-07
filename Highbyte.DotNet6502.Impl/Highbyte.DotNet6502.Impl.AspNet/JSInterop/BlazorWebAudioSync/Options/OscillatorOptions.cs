// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

public class OscillatorOptions
{
    public OscillatorType Type { get; set; } = OscillatorType.Sine;
    public float Frequency { get; set; } = 440;
    public float Detune { get; set; } = 0;

    // Missing PeriodicWave for now.
}
