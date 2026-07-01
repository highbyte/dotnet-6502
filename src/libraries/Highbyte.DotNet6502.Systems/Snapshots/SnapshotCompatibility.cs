namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Result of validating a <see cref="SnapshotManifest"/> against a target system's snapshot
/// provider. When <see cref="IsCompatible"/> is false, <see cref="Reason"/> explains why the
/// snapshot cannot be restored.
/// </summary>
public sealed class SnapshotCompatibility
{
    public bool IsCompatible { get; }
    public string? Reason { get; }
    public IReadOnlyList<string> Warnings { get; }

    private SnapshotCompatibility(bool isCompatible, string? reason, IReadOnlyList<string>? warnings)
    {
        IsCompatible = isCompatible;
        Reason = reason;
        Warnings = warnings ?? Array.Empty<string>();
    }

    public static SnapshotCompatibility Compatible(IReadOnlyList<string>? warnings = null)
        => new(true, null, warnings);

    public static SnapshotCompatibility Incompatible(string reason)
        => new(false, reason, null);
}
