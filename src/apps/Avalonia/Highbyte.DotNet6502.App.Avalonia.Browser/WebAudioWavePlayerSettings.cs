using System.Runtime.Versioning;

namespace Highbyte.DotNet6502.App.Avalonia.Browser;

/// <summary>
/// Configuration settings for WebAudioWavePlayer that control latency and stability trade-offs.
/// </summary>
[SupportedOSPlatform("browser")]
public class WebAudioWavePlayerSettings
{
    /// <summary>
    /// Gets or sets the desired latency in milliseconds.
    /// This controls the size of audio buffers sent from C# to JavaScript.
    /// Lower values = lower latency but higher CPU usage and risk of glitches.
    /// Default: 50ms
    /// </summary>
    public int DesiredLatencyMs { get; set; } = 50;

    /// <summary>
    /// Gets or sets the ring buffer capacity multiplier.
    /// The JavaScript ring buffer capacity = DesiredLatency * this multiplier.
    /// Higher values = more safety margin but higher latency.
    /// Default: 3.0 (ring buffer holds 3x the C# buffer size)
    /// </summary>
    public double RingBufferCapacityMultiplier { get; set; } = 3.0;

    /// <summary>
    /// Gets or sets the minimum buffer multiplier before playback starts.
    /// Playback starts when buffered samples >= DesiredLatency * this multiplier.
    /// Lower values = faster startup but more risk of initial underruns.
    /// Default: 1.0 (start after buffering 1x the C# buffer size)
    /// </summary>
    public double MinBufferBeforePlayMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the ScriptProcessor buffer size in samples.
    /// Must be a power of 2 between 256 and 16384.
    /// Lower values = lower latency but higher CPU usage.
    /// Default: 2048 (~46ms at 44100Hz)
    /// </summary>
    public int ScriptProcessorBufferSize { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the interval in milliseconds between stats logging.
    /// Set to 0 to disable stats logging.
    /// Default: 10000 (10 seconds)
    /// </summary>
    public int StatsIntervalMs { get; set; } = 10000;

    /// <summary>
    /// Predefined profile for lowest possible latency (requires powerful CPU, may have glitches).
    /// Total latency: ~30-50ms
    /// Warning: This profile is aggressive and may cause audio glitches on most systems.
    /// </summary>
    public static WebAudioWavePlayerSettings LowestLatency => new()
    {
        DesiredLatencyMs = 15,
        RingBufferCapacityMultiplier = 2.0,
        MinBufferBeforePlayMultiplier = 0.3,
        ScriptProcessorBufferSize = 512,
        StatsIntervalMs = 10000
    };

    /// <summary>
    /// Predefined profile for low latency (may have glitches on slower systems).
    /// Total latency: ~60-80ms
    /// </summary>
    public static WebAudioWavePlayerSettings LowLatency => new()
    {
        DesiredLatencyMs = 30,
        RingBufferCapacityMultiplier = 2.5,
        MinBufferBeforePlayMultiplier = 0.5,
        ScriptProcessorBufferSize = 1024,
        StatsIntervalMs = 10000
    };

    /// <summary>
    /// Predefined profile for balanced latency and stability (default).
    /// Total latency: ~100-150ms
    /// </summary>
    public static WebAudioWavePlayerSettings Balanced => new()
    {
        DesiredLatencyMs = 50,
        RingBufferCapacityMultiplier = 3.0,
        MinBufferBeforePlayMultiplier = 1.0,
        ScriptProcessorBufferSize = 2048,
        StatsIntervalMs = 10000
    };

    /// <summary>
    /// Predefined profile for high stability (higher latency but fewer glitches).
    /// Total latency: ~200-300ms
    /// </summary>
    public static WebAudioWavePlayerSettings HighStability => new()
    {
        DesiredLatencyMs = 100,
        RingBufferCapacityMultiplier = 4.0,
        MinBufferBeforePlayMultiplier = 1.5,
        ScriptProcessorBufferSize = 4096,
        StatsIntervalMs = 10000
    };

    /// <summary>
    /// Predefined profile for maximum stability (highest latency, minimal glitches).
    /// Total latency: ~400-500ms
    /// </summary>
    public static WebAudioWavePlayerSettings MaxStability => new()
    {
        DesiredLatencyMs = 200,
        RingBufferCapacityMultiplier = 4.0,
        MinBufferBeforePlayMultiplier = 2.0,
        ScriptProcessorBufferSize = 4096,
        StatsIntervalMs = 10000
    };
}
