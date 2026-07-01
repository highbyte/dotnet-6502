namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Options controlling how a snapshot is captured. Minimal in the MVP; media-embedding and
/// reference-only modes will be added when `.d64`/`.crt` support lands.
/// </summary>
public sealed class SnapshotSaveOptions
{
    public static readonly SnapshotSaveOptions Default = new();

    /// <summary>Optional emulator build/commit identifier recorded in the manifest for diagnostics.</summary>
    public string? EmulatorVersion { get; set; }
    public string? EmulatorCommit { get; set; }

    /// <summary>
    /// Optional machine configuration variant recorded in the manifest. Used on restore to rebuild
    /// the target system with the same variant before restoring module state.
    /// </summary>
    public string? ConfigurationVariant { get; set; }

    /// <summary>Optional human-readable machine model recorded in the manifest for diagnostics.</summary>
    public string? Model { get; set; }

    /// <summary>
    /// Optional runtime-settings ("config") blocks to embed in the snapshot, captured by the host
    /// orchestration when the user opts in ("Include config in save"). Null/empty writes no config,
    /// keeping the snapshot machine-state-only. See <see cref="SnapshotConfigContent"/>.
    /// </summary>
    public SnapshotConfigContent? Config { get; set; }
}
