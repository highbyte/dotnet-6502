using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Systems.Recording;

public sealed class DemoManifest
{
    public int FormatVersion { get; set; } = 1;
    public string SystemName { get; set; } = "";
    public string Schema { get; set; } = "";
    public int SchemaVersion { get; set; }
    public string CompatibilityId { get; set; } = "";
    public string? ConfigurationVariant { get; set; }
    public double FramesPerSecond { get; set; }
    public long TotalFrames { get; set; }
    public string SnapshotSha256 { get; set; } = "";
    public string TimelineSha256 { get; set; } = "";
    public string InitialStateDigest { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DemoManifest))]
internal partial class DemoJsonContext : JsonSerializerContext;
