using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// The small, human-readable <c>snapshot.json</c> stored at the root of a <c>.d6502snap</c>
/// package. Describes the machine the snapshot belongs to and the ordered set of binary
/// modules (and, in later versions, embedded media) that make up the saved state.
/// </summary>
public class SnapshotManifest
{
    /// <summary>The on-disk snapshot container format version this code reads/writes.</summary>
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public DateTimeOffset CreatedUtc { get; set; }
    public SnapshotEmulatorInfo Emulator { get; set; } = new();
    public SnapshotMachineInfo Machine { get; set; } = new();
    public List<SnapshotModuleEntry> Modules { get; set; } = new();

    /// <summary>Embedded/attached media. Unused in the MVP, reserved for `.d64`/`.crt` support.</summary>
    public List<SnapshotMediaEntry> Media { get; set; } = new();
}

public class SnapshotEmulatorInfo
{
    public string Name { get; set; } = "dotnet-6502";
    public string Version { get; set; } = "0.0.0";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Commit { get; set; }
}

public class SnapshotMachineInfo
{
    public string SystemName { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConfigurationVariant { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; set; }

    /// <summary>The machine-specific snapshot version (see <see cref="SnapshotMachineId.SupportedSnapshotVersion"/>).</summary>
    public int SnapshotVersion { get; set; }
}

public class SnapshotModuleEntry
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Path { get; set; } = "";
    public bool Required { get; set; }
}

public class SnapshotMediaEntry
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Mode { get; set; } = "embedded";
    public string? Path { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUri { get; set; }
    public string? Sha256 { get; set; }
}
