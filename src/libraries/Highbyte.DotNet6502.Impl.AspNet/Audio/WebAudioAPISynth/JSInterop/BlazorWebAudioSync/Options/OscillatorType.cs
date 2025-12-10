// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync.Options;

public enum OscillatorType
{
    Sine,
    Square,
    Sawtooth,
    Triangle,
    Custom
}

public static class OscillatorTypeExtensions
{
    public static string AsString(this OscillatorType type) =>
        type switch
        {
            OscillatorType.Sine => "sine",
            OscillatorType.Square => "square",
            OscillatorType.Sawtooth => "sawtooth",
            OscillatorType.Triangle => "triangle",
            OscillatorType.Custom => "custom",
            _ => throw new ArgumentException($"Value '{type}' was not a valid OscillatorType.")
        };
}
