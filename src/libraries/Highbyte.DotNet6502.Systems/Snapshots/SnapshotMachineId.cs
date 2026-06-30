namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Identifies which emulated machine a snapshot belongs to, and the snapshot-format
/// version that machine's snapshot provider currently understands.
///
/// <para>
/// <see cref="SystemName"/> must match <see cref="ISystem.Name"/> of the system that owns
/// the provider (for example "Generic", "C64", "Vic20"). It is compared against the
/// <see cref="SnapshotManifest.Machine"/> system name on restore to reject loading a
/// snapshot into the wrong machine.
/// </para>
/// </summary>
public readonly record struct SnapshotMachineId(string SystemName, int SupportedSnapshotVersion);
