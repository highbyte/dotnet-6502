namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

/// <summary>
/// Options for a special form of oscillator that can be used to modulate an oscillator with pulse width.
/// Note: This is not a standard WebAudio options, but a custom implementation.
/// </summary>
public class CustomPulseOscillatorOptions
{
    public float Frequency { get; set; } = 440;
    public float Detune { get; set; } = 0;

    public float DefaultWidth { get; set; } = 0;
}
