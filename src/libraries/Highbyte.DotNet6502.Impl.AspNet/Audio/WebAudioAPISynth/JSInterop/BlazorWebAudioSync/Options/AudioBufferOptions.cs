using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync.Options;

public class AudioBufferOptions
{
    [JsonPropertyName("length")]
    public int Length { get; set; }

    [JsonPropertyName("numberOfChannels")]
    public int NumberOfChannels { get; set; } = 1;

    [JsonPropertyName("sampleRate")]
    public float SampleRate { get; set; }

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
