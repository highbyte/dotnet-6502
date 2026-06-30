namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Context passed to <see cref="ISnapshotModule.Capture"/>. Gives a module access to the
/// system being captured and a place to record non-fatal warnings.
/// </summary>
public sealed class SnapshotCaptureContext
{
    public ISystem System { get; }
    public SnapshotSaveOptions Options { get; }
    public List<string> Warnings { get; } = new();

    public SnapshotCaptureContext(ISystem system, SnapshotSaveOptions options)
    {
        System = system;
        Options = options;
    }

    public void AddWarning(string message) => Warnings.Add(message);
}

/// <summary>
/// Context passed to <see cref="ISnapshotModule.Restore"/>. Gives a module access to the
/// freshly built target system and a place to record non-fatal warnings (e.g. state that
/// could not be reproduced exactly).
/// </summary>
public sealed class SnapshotRestoreContext
{
    public ISystem System { get; }
    public SnapshotManifest Manifest { get; }
    public List<string> Warnings { get; } = new();

    public SnapshotRestoreContext(ISystem system, SnapshotManifest manifest)
    {
        System = system;
        Manifest = manifest;
    }

    public void AddWarning(string message) => Warnings.Add(message);
}
