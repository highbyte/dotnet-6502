namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Base exception for all emulator state snapshot save/restore errors.
/// </summary>
public class SnapshotException : DotNet6502Exception
{
    public SnapshotException(string message) : base(message)
    {
    }
}

/// <summary>
/// Thrown when a snapshot cannot be restored into the target system because of a
/// machine, module, or version incompatibility. See <see cref="Compatibility"/> for details.
/// </summary>
public class SnapshotIncompatibleException : SnapshotException
{
    public SnapshotCompatibility Compatibility { get; }

    public SnapshotIncompatibleException(SnapshotCompatibility compatibility)
        : base(compatibility.Reason ?? "Snapshot is incompatible with the target system.")
    {
        Compatibility = compatibility;
    }
}
