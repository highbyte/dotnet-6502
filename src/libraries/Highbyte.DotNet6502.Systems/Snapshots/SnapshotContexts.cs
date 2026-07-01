namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// A media file (e.g. a <c>.d64</c> disk image or <c>.crt</c> cartridge image) that a module wants
/// embedded in the snapshot package's <c>media/</c> folder. The <see cref="SnapshotService"/> writes
/// the bytes, records a <see cref="SnapshotMediaEntry"/> in the manifest (with a SHA-256), and makes
/// the bytes available again on restore keyed by <see cref="Id"/>.
/// </summary>
public sealed class SnapshotEmbeddedMedia
{
    public string Id { get; }
    public string Kind { get; }
    public string? SourceName { get; }
    public byte[] Bytes { get; }

    public SnapshotEmbeddedMedia(string id, string kind, string? sourceName, byte[] bytes)
    {
        Id = id;
        Kind = kind;
        SourceName = sourceName;
        Bytes = bytes;
    }
}

/// <summary>
/// Context passed to <see cref="ISnapshotModule.Capture"/>. Gives a module access to the
/// system being captured, a place to record non-fatal warnings, and a way to register media files
/// (disk/cartridge images) to embed in the package.
/// </summary>
public sealed class SnapshotCaptureContext
{
    public ISystem System { get; }
    public SnapshotSaveOptions Options { get; }
    public List<string> Warnings { get; } = new();
    public List<SnapshotEmbeddedMedia> EmbeddedMedia { get; } = new();

    public SnapshotCaptureContext(ISystem system, SnapshotSaveOptions options)
    {
        System = system;
        Options = options;
    }

    public void AddWarning(string message) => Warnings.Add(message);

    /// <summary>Registers a media file to embed in the package (see <see cref="SnapshotEmbeddedMedia"/>).</summary>
    public void AddEmbeddedMedia(string id, string kind, string? sourceName, byte[] bytes)
        => EmbeddedMedia.Add(new SnapshotEmbeddedMedia(id, kind, sourceName, bytes));
}

/// <summary>
/// Context passed to <see cref="ISnapshotModule.Restore"/>. Gives a module access to the
/// freshly built target system and a place to record non-fatal warnings (e.g. state that
/// could not be reproduced exactly).
/// </summary>
public sealed class SnapshotRestoreContext
{
    private readonly IReadOnlyDictionary<string, byte[]> _embeddedMedia;

    public ISystem System { get; }
    public SnapshotManifest Manifest { get; }
    public List<string> Warnings { get; } = new();

    public SnapshotRestoreContext(
        ISystem system,
        SnapshotManifest manifest,
        IReadOnlyDictionary<string, byte[]> embeddedMedia)
    {
        System = system;
        Manifest = manifest;
        _embeddedMedia = embeddedMedia;
    }

    public void AddWarning(string message) => Warnings.Add(message);

    /// <summary>
    /// Gets the embedded media bytes registered under <paramref name="id"/> during capture, if the
    /// snapshot included them (see <see cref="SnapshotCaptureContext.AddEmbeddedMedia"/>).
    /// </summary>
    public bool TryGetEmbeddedMedia(string id, out byte[] bytes)
        => _embeddedMedia.TryGetValue(id, out bytes!);
}
