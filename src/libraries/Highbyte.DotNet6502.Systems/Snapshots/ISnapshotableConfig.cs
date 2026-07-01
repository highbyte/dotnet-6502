namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Opt-in capability implemented by a settings owner that can serialize a curated subset of its
/// current runtime settings into a snapshot and apply them back on restore. Implemented by a global
/// <c>ISystemConfig</c> for the portable system-config block. See the config extension in the feature
/// design doc.
///
/// <para>The shared snapshot framework treats the returned payload as an opaque string — it never
/// enumerates individual fields — so adding a setting touches only its owner.</para>
/// </summary>
public interface ISnapshotableConfig
{
    /// <summary>
    /// Serializes the owner's snapshot-relevant settings (current values) to an opaque JSON string,
    /// or null if there is nothing to capture.
    /// </summary>
    string? ExportSnapshotSettings();

    /// <summary>
    /// Applies a payload previously produced by <see cref="ExportSnapshotSettings"/>. The owner
    /// tolerates unknown/missing fields (forward/backward compatible). For a system config this is
    /// applied to the config <i>before</i> the machine is rebuilt on restore, so the rebuilt machine
    /// reflects it — no live-machine poke required.
    /// </summary>
    void ApplySnapshotSettings(string payload);
}
