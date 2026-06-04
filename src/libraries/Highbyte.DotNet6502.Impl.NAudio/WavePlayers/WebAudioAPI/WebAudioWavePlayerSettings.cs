using System.Runtime.Versioning;

namespace Highbyte.DotNet6502.Impl.NAudio.WavePlayers.WebAudioAPI;

/// <summary>
/// Configuration settings for WebAudioWavePlayer that control latency and stability trade-offs.
/// </summary>
[SupportedOSPlatform("browser")]
public class WebAudioWavePlayerSettings
{
    public WavePlayerSettingsProfile ProfileType { get; set; } = WavePlayerSettingsProfile.Balanced;
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
    /// Gets or sets the number of samples accumulated by the direct-write browser target before
    /// flushing to JavaScript. Lower values reduce producer-side batch latency but increase JS
    /// interop frequency.
    /// Default: 512 samples (~11.6ms at 44100Hz)
    /// </summary>
    public int DirectWriteFlushSamples { get; set; } = 512;

    /// <summary>
    /// Gets or sets the desired latency used by the direct-write browser target.
    /// Set to 0 to use <see cref="DesiredLatencyMs"/>.
    /// </summary>
    public int DirectWriteDesiredLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the ring buffer capacity multiplier used by the direct-write browser target.
    /// Set to 0 to use <see cref="RingBufferCapacityMultiplier"/>.
    /// </summary>
    public double DirectWriteRingBufferCapacityMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the minimum buffer multiplier used by the direct-write browser target.
    /// Set to 0 to use <see cref="MinBufferBeforePlayMultiplier"/>.
    /// </summary>
    public double DirectWriteMinBufferBeforePlayMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the ScriptProcessor buffer size used by the direct-write browser target.
    /// Set to 0 to use <see cref="ScriptProcessorBufferSize"/>.
    /// </summary>
    public int DirectWriteScriptProcessorBufferSize { get; set; }

    /// <summary>
    /// Gets or sets the interval in milliseconds between stats logging.
    /// Set to 0 to disable stats logging.
    /// Default: 10000 (10 seconds)
    /// </summary>
    public int StatsIntervalMs { get; set; } = 10000;


    public static WebAudioWavePlayerSettings GetSettingsForProfile(WavePlayerSettingsProfile profile) => profile switch
    {
        WavePlayerSettingsProfile.LowestLatency => LowestLatency,
        WavePlayerSettingsProfile.LowLatency => LowLatency,
        WavePlayerSettingsProfile.Balanced => Balanced,
        WavePlayerSettingsProfile.HighStability => HighStability,
        WavePlayerSettingsProfile.MaxStability => MaxStability,
        _ => Balanced,
    };

    /// <summary>
    /// Predefined profile for lowest possible latency (requires powerful CPU, may have glitches).
    /// Total latency: ~30-50ms
    /// Warning: This profile is aggressive and may cause audio glitches on most systems.
    /// </summary>
    public static WebAudioWavePlayerSettings LowestLatency => new()
    {
        ProfileType = WavePlayerSettingsProfile.LowestLatency,
        DesiredLatencyMs = 15,
        RingBufferCapacityMultiplier = 2.0,
        MinBufferBeforePlayMultiplier = 0.3,
        ScriptProcessorBufferSize = 512,
        DirectWriteFlushSamples = 256,
        DirectWriteDesiredLatencyMs = 15,
        DirectWriteRingBufferCapacityMultiplier = 2.0,
        DirectWriteMinBufferBeforePlayMultiplier = 0.3,
        DirectWriteScriptProcessorBufferSize = 512,
        StatsIntervalMs = 10000
    };

    /// <summary>
    /// Predefined profile for low latency (may have glitches on slower systems).
    /// Total latency: ~60-80ms
    /// </summary>
    public static WebAudioWavePlayerSettings LowLatency => new()
    {
        ProfileType = WavePlayerSettingsProfile.LowLatency,
        DesiredLatencyMs = 30,
        RingBufferCapacityMultiplier = 2.5,
        MinBufferBeforePlayMultiplier = 0.5,
        ScriptProcessorBufferSize = 1024,
        DirectWriteFlushSamples = 512,
        DirectWriteDesiredLatencyMs = 25,
        DirectWriteRingBufferCapacityMultiplier = 2.0,
        DirectWriteMinBufferBeforePlayMultiplier = 0.5,
        DirectWriteScriptProcessorBufferSize = 512,
        StatsIntervalMs = 10000
    };

    /// <summary>
    /// Predefined profile for balanced latency and stability (default).
    /// Total latency: ~100-150ms
    /// </summary>
    public static WebAudioWavePlayerSettings Balanced => new()
    {
        ProfileType = WavePlayerSettingsProfile.Balanced,
        DesiredLatencyMs = 50,
        RingBufferCapacityMultiplier = 3.0,
        MinBufferBeforePlayMultiplier = 1.0,
        ScriptProcessorBufferSize = 2048,
        DirectWriteFlushSamples = 128,
        DirectWriteDesiredLatencyMs = 25,
        DirectWriteRingBufferCapacityMultiplier = 2.0,
        DirectWriteMinBufferBeforePlayMultiplier = 0.5,
        DirectWriteScriptProcessorBufferSize = 1024,
        StatsIntervalMs = 10000
    };

    /// <summary>
    /// Predefined profile for high stability (higher latency but fewer glitches).
    /// Total latency: ~200-300ms
    /// </summary>
    public static WebAudioWavePlayerSettings HighStability => new()
    {
        ProfileType = WavePlayerSettingsProfile.HighStability,
        DesiredLatencyMs = 100,
        RingBufferCapacityMultiplier = 4.0,
        MinBufferBeforePlayMultiplier = 1.5,
        ScriptProcessorBufferSize = 4096,
        DirectWriteFlushSamples = 1024,
        DirectWriteDesiredLatencyMs = 60,
        DirectWriteRingBufferCapacityMultiplier = 3.0,
        DirectWriteMinBufferBeforePlayMultiplier = 1.0,
        DirectWriteScriptProcessorBufferSize = 2048,
        StatsIntervalMs = 10000
    };

    /// <summary>
    /// Predefined profile for maximum stability (highest latency, minimal glitches).
    /// Total latency: ~400-500ms
    /// </summary>
    public static WebAudioWavePlayerSettings MaxStability => new()
    {
        ProfileType = WavePlayerSettingsProfile.MaxStability,
        DesiredLatencyMs = 200,
        RingBufferCapacityMultiplier = 4.0,
        MinBufferBeforePlayMultiplier = 2.0,
        ScriptProcessorBufferSize = 4096,
        DirectWriteFlushSamples = 2048,
        DirectWriteDesiredLatencyMs = 100,
        DirectWriteRingBufferCapacityMultiplier = 4.0,
        DirectWriteMinBufferBeforePlayMultiplier = 1.5,
        DirectWriteScriptProcessorBufferSize = 4096,
        StatsIntervalMs = 10000
    };
}
