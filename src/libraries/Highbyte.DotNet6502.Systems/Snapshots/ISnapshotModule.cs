namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// A single named, versioned unit of emulator state that can be written to and read back
/// from a snapshot package (e.g. the shared <c>cpu-6502</c> module, or a system-specific
/// memory/chip module).
/// </summary>
public interface ISnapshotModule
{
    /// <summary>Stable module name, used as the manifest key and the <c>modules/&lt;name&gt;.bin</c> file name.</summary>
    string Name { get; }

    /// <summary>
    /// Module payload version. Bumped when the binary layout changes. On restore a snapshot
    /// whose stored version is newer than this is rejected (strict v1 compatibility).
    /// </summary>
    int Version { get; }

    /// <summary>
    /// True if the module must be present to restore the machine. A snapshot that omits a
    /// required module the provider declares, or includes a required module the provider does
    /// not recognise, is rejected. Optional modules may be absent or unknown.
    /// </summary>
    bool Required { get; }

    void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context);
    void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context);
}
