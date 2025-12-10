using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync.Options;

public class AudioBufferSourceNodeOptions
{
    [JsonPropertyName("audioBuffer")]
    public AudioBufferSync? Buffer { get; set; }

    [JsonPropertyName("detune")]
    public float Detune { get; set; } = 0;

    [JsonPropertyName("loop")]
    public bool Loop { get; set; } = false;

    [JsonPropertyName("loopEnd")]
    public int LoopEnd { get; set; } = 0;

    [JsonPropertyName("loopStart")]
    public int LoopStart { get; set; } = 0;

    [JsonPropertyName("playbackRate")]
    public float PlaybackRate { get; set; } = 1;

    // Ignored if channelCountMode = max
    [JsonPropertyName("channelCount")]
    public int ChannelCount { get; set; }

    // max, clamped-max, clamped-max
    [JsonPropertyName("channelCountMode")]
    public string? ChannelCountMode { get; set; } = "max";

    // speakers, discrete
    [JsonPropertyName("channelInterpretation")]
    public string? ChannelInterpretation { get; set; } = "speakers";
}
