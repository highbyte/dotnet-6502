using System.Reflection;
using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Systems.Recording;

public sealed class DemoManifest
{
    public const int CurrentFormatVersion = 2;

    // Source identity also distinguishes local builds with uncommitted engine changes.
    public static readonly string EngineSourceId = typeof(DemoManifest).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "ReplayEngineBuildId").Value!;
    internal static readonly string RunningBuildId =
        $"{typeof(DemoManifest).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion} ({EngineSourceId})";

    public int FormatVersion { get; set; }
    public string? RecordedBuildId { get; set; }
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
