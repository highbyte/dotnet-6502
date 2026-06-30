namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Outcome of a successful <see cref="SnapshotService.Restore"/>. The system is left paused;
/// the caller decides when to resume. <see cref="Warnings"/> contains non-fatal issues such
/// as ignored unknown optional modules or state that could not be reproduced exactly.
/// </summary>
public sealed class SnapshotRestoreResult
{
    public SnapshotManifest Manifest { get; }
    public IReadOnlyList<string> Warnings { get; }

    public SnapshotRestoreResult(SnapshotManifest manifest, IReadOnlyList<string> warnings)
    {
        Manifest = manifest;
        Warnings = warnings;
    }
}
