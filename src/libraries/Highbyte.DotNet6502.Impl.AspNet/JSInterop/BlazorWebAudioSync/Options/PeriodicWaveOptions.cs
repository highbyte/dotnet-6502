using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

public class PeriodicWaveOptions : PeriodicWaveConstraints
{
    [JsonPropertyName("real")]
    public float[] Real { get; set; } = new float[0];

    [JsonPropertyName("imag")]
    public float[] Imag { get; set; } = new float[0];

    // TODO: Implement channel-properties?
    //[JsonPropertyName("channelCount")]
    //public int ChannelCount { get; set; }

    //[JsonPropertyName("channelCountMode")]
    //public string ChannelCountMode { get; set; }

    //[JsonPropertyName("channelInterpretation")]
    //public string ChannelInterpretation { get; set; }
}
