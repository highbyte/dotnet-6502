// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------


// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync.Options;

public class OscillatorOptions
{
    public OscillatorType Type { get; set; } = OscillatorType.Sine;
    public float Frequency { get; set; } = 440;
    public float Detune { get; set; } = 0;

    // Note: Set PeriodicWave using OscillatorNodeSync.SetPeriodicWave(wave) instead
    /// <summary>
    /// PeriodicWave is used when Type is set to Custom.
    /// </summary>
    public PeriodicWaveSync? PeriodicWave { get; set; }
}
