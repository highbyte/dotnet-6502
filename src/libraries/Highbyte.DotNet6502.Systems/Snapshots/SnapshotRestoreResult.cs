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

    /// <summary>
    /// The runtime-settings ("config") blocks embedded in the snapshot, or null if none. Machine state
    /// is always restored; whether these settings are <i>applied</i> is a separate host decision
    /// ("Restore config on load"). See <see cref="SnapshotConfigContent"/>.
    /// </summary>
    public SnapshotConfigContent? Config { get; }

    public SnapshotRestoreResult(SnapshotManifest manifest, IReadOnlyList<string> warnings, SnapshotConfigContent? config = null)
    {
        Manifest = manifest;
        Warnings = warnings;
        Config = config;
    }
}
