namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Opt-in capability implemented by a system (or exposed by it) that can be saved to and
/// restored from a snapshot. A system advertises the ordered set of modules required to
/// capture/restore its state and validates that an incoming snapshot is compatible.
/// </summary>
public interface ISystemSnapshotProvider
{
    SnapshotMachineId MachineId { get; }

    /// <summary>
    /// The ordered set of snapshot modules for this machine. Required modules are captured
    /// and restored first, optional modules afterwards. The list is consulted both when
    /// capturing (every module is written) and when restoring (to resolve manifest entries
    /// back to module implementations).
    /// </summary>
    IReadOnlyList<ISnapshotModule> GetSnapshotModules();

    /// <summary>
    /// Machine-specific compatibility check (model/config/variant). The shared
    /// <see cref="SnapshotService"/> already enforces format-version, machine-name, unknown
    /// required module, and module-version rules, so implementations only add machine-level
    /// checks on top.
    /// </summary>
    SnapshotCompatibility ValidateSnapshot(SnapshotManifest manifest);
}
