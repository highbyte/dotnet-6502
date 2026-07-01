using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Host-level orchestration for saving and restoring emulator state snapshots.
///
/// <para>
/// A snapshot is a zip-based <c>.d6502snap</c> package containing a human-readable
/// <c>snapshot.json</c> manifest and one <c>modules/&lt;name&gt;.bin</c> entry per module.
/// The caller is responsible for pausing the emulator around these calls (snapshots should be
/// taken between CPU instructions); the service itself only reads/writes state, it does not
/// start or stop execution.
/// </para>
/// </summary>
public sealed class SnapshotService
{
    public const string ManifestEntryName = "snapshot.json";
    public const string ModulesDirectory = "modules";
    public const string MediaDirectory = "media";
    public const string FileExtension = ".d6502snap";

    // Optional runtime-settings blocks (see SnapshotConfigContent). Stored as opaque, owner-serialized
    // JSON entries; the framework never interprets their contents.
    public const string ConfigSystemEntryName = "config/systemConfig.json";
    public const string ConfigHostEntryName = "config/host.json";

    // Source-generated metadata (see SnapshotManifestJsonContext) is used instead of reflection-based
    // serialization so the manifest round-trips in trimmed/AOT hosts (e.g. the Browser/WASM app).

    /// <summary>
    /// Captures <paramref name="system"/> state into a snapshot package written to
    /// <paramref name="output"/>. The system must implement <see cref="ISystemSnapshotProvider"/>.
    /// </summary>
    public void Save(ISystem system, Stream output, SnapshotSaveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(output);
        options ??= SnapshotSaveOptions.Default;

        var provider = GetProvider(system);
        var captureContext = new SnapshotCaptureContext(system, options);

        var manifest = new SnapshotManifest
        {
            FormatVersion = SnapshotManifest.CurrentFormatVersion,
            CreatedUtc = DateTimeOffset.UtcNow,
            Emulator = new SnapshotEmulatorInfo
            {
                Version = options.EmulatorVersion ?? "0.0.0",
                Commit = options.EmulatorCommit,
            },
            Machine = new SnapshotMachineInfo
            {
                SystemName = system.Name,
                ConfigurationVariant = options.ConfigurationVariant,
                Model = options.Model,
                SnapshotVersion = provider.MachineId.SupportedSnapshotVersion,
            },
        };

        // Capture each module to its own in-memory buffer first, so the manifest can be
        // written before the module entries.
        var moduleData = new List<(SnapshotModuleEntry entry, byte[] bytes)>();
        foreach (var module in provider.GetSnapshotModules())
        {
            using var moduleStream = new MemoryStream();
            var writer = new SnapshotModuleWriter(moduleStream);
            module.Capture(writer, captureContext);

            var entry = new SnapshotModuleEntry
            {
                Name = module.Name,
                Version = module.Version,
                Required = module.Required,
                Path = $"{ModulesDirectory}/{module.Name}.bin",
            };
            manifest.Modules.Add(entry);
            moduleData.Add((entry, moduleStream.ToArray()));
        }

        // Media (disk/cartridge images) the modules registered during capture. Embedded in the
        // package so the snapshot is self-contained.
        var mediaData = new List<(string path, byte[] bytes)>();
        foreach (var media in captureContext.EmbeddedMedia)
        {
            var path = $"{MediaDirectory}/{media.Id}.{media.Kind}";
            manifest.Media.Add(new SnapshotMediaEntry
            {
                Id = media.Id,
                Kind = media.Kind,
                Mode = "embedded",
                Path = path,
                SourceName = media.SourceName,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(media.Bytes)),
            });
            mediaData.Add((path, media.Bytes));
        }

        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        using (var manifestStream = manifestEntry.Open())
        {
            JsonSerializer.Serialize(manifestStream, manifest, SnapshotManifestJsonContext.Default.SnapshotManifest);
        }

        foreach (var (entry, bytes) in moduleData)
        {
            var moduleEntry = archive.CreateEntry(entry.Path, CompressionLevel.Optimal);
            using var entryStream = moduleEntry.Open();
            entryStream.Write(bytes, 0, bytes.Length);
        }

        foreach (var (path, bytes) in mediaData)
        {
            var mediaEntry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using var entryStream = mediaEntry.Open();
            entryStream.Write(bytes, 0, bytes.Length);
        }

        // Optional runtime-settings ("config") blocks — opaque, owner-serialized JSON.
        if (options.Config is { IsEmpty: false } config)
        {
            WriteTextEntry(archive, ConfigSystemEntryName, config.SystemConfigJson);
            WriteTextEntry(archive, ConfigHostEntryName, config.HostJson);
        }
    }

    private static void WriteTextEntry(ZipArchive archive, string entryName, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        entryStream.Write(bytes, 0, bytes.Length);
    }

    private static string? ReadTextEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry == null)
            return null;
        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads the optional runtime-settings ("config") blocks from a snapshot package, or null if the
    /// snapshot contains none. Leaves the archive readable.
    /// </summary>
    private static SnapshotConfigContent? ReadConfig(ZipArchive archive)
    {
        var systemJson = ReadTextEntry(archive, ConfigSystemEntryName);
        var hostJson = ReadTextEntry(archive, ConfigHostEntryName);
        if (string.IsNullOrEmpty(systemJson) && string.IsNullOrEmpty(hostJson))
            return null;
        return new SnapshotConfigContent { SystemConfigJson = systemJson, HostJson = hostJson };
    }

    /// <summary>
    /// Restores a snapshot read from <paramref name="input"/> into <paramref name="targetSystem"/>,
    /// which must be a freshly built system of the matching machine implementing
    /// <see cref="ISystemSnapshotProvider"/>. The system is left paused on success.
    /// </summary>
    /// <exception cref="SnapshotIncompatibleException">The snapshot is incompatible with the target.</exception>
    /// <exception cref="SnapshotException">The package is malformed.</exception>
    public SnapshotRestoreResult Restore(ISystem targetSystem, Stream input)
    {
        ArgumentNullException.ThrowIfNull(targetSystem);
        ArgumentNullException.ThrowIfNull(input);

        var provider = GetProvider(targetSystem);

        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);

        var manifest = ReadManifest(archive);
        EnforceCompatibility(manifest, targetSystem, provider, out var modulesByName);

        var embeddedMedia = ReadEmbeddedMedia(archive, manifest);
        var restoreContext = new SnapshotRestoreContext(targetSystem, manifest, embeddedMedia);
        var providerWarnings = provider.ValidateSnapshot(manifest).Warnings;
        foreach (var warning in providerWarnings)
            restoreContext.AddWarning(warning);

        // Restore required modules first, then optional ones (per the lifecycle in the design doc).
        foreach (var entry in manifest.Modules.OrderByDescending(m => m.Required))
        {
            if (!modulesByName.TryGetValue(entry.Name, out var module))
            {
                // Unknown optional module: ignore with a warning. (Unknown required modules
                // were already rejected in EnforceCompatibility.)
                restoreContext.AddWarning($"Ignoring unknown optional module '{entry.Name}'.");
                continue;
            }

            var bytes = ReadModuleBytes(archive, entry);
            using var moduleStream = new MemoryStream(bytes, writable: false);
            var reader = new SnapshotModuleReader(moduleStream);
            module.Restore(reader, restoreContext);
        }

        // Machine state is now restored. Any embedded runtime-settings ("config") blocks are returned
        // to the caller opaquely; whether to apply them is the host's decision.
        var config = ReadConfig(archive);

        return new SnapshotRestoreResult(manifest, restoreContext.Warnings, config);
    }

    /// <summary>
    /// Reads just the manifest from a snapshot package without restoring anything. Useful for a
    /// host to determine the target machine/variant before building a system to restore into.
    /// Leaves <paramref name="input"/> open; the caller should reset its position before restoring.
    /// </summary>
    public static SnapshotManifest PeekManifest(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        return ReadManifest(archive);
    }

    private static ISystemSnapshotProvider GetProvider(ISystem system)
    {
        if (system is ISystemSnapshotProvider provider)
            return provider;
        throw new SnapshotException(
            $"System '{system.Name}' does not support snapshots (no {nameof(ISystemSnapshotProvider)}).");
    }

    private static SnapshotManifest ReadManifest(ZipArchive archive)
    {
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new SnapshotException($"Snapshot package is missing '{ManifestEntryName}'.");
        using var stream = manifestEntry.Open();
        var manifest = JsonSerializer.Deserialize(stream, SnapshotManifestJsonContext.Default.SnapshotManifest)
            ?? throw new SnapshotException($"Snapshot '{ManifestEntryName}' could not be parsed.");
        return manifest;
    }

    private static Dictionary<string, byte[]> ReadEmbeddedMedia(ZipArchive archive, SnapshotManifest manifest)
    {
        var media = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in manifest.Media)
        {
            if (!string.Equals(entry.Mode, "embedded", StringComparison.Ordinal) || string.IsNullOrEmpty(entry.Path))
                continue; // reference-only media is not embedded; nothing to read here.

            var mediaEntry = archive.GetEntry(entry.Path)
                ?? throw new SnapshotException($"Snapshot package is missing embedded media '{entry.Path}'.");
            using var stream = mediaEntry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            media[entry.Id] = ms.ToArray();
        }
        return media;
    }

    private static byte[] ReadModuleBytes(ZipArchive archive, SnapshotModuleEntry entry)
    {
        var moduleEntry = archive.GetEntry(entry.Path)
            ?? throw new SnapshotException($"Snapshot package is missing module entry '{entry.Path}'.");
        using var stream = moduleEntry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Applies the shared strict-compatibility rules (format version, machine name, unknown
    /// required modules, newer module versions) and the provider's machine-specific check.
    /// </summary>
    private static void EnforceCompatibility(
        SnapshotManifest manifest,
        ISystem targetSystem,
        ISystemSnapshotProvider provider,
        out Dictionary<string, ISnapshotModule> modulesByName)
    {
        if (manifest.FormatVersion > SnapshotManifest.CurrentFormatVersion)
            throw new SnapshotIncompatibleException(SnapshotCompatibility.Incompatible(
                $"Snapshot format version {manifest.FormatVersion} is newer than supported version {SnapshotManifest.CurrentFormatVersion}."));

        if (!string.Equals(manifest.Machine.SystemName, targetSystem.Name, StringComparison.Ordinal))
            throw new SnapshotIncompatibleException(SnapshotCompatibility.Incompatible(
                $"Snapshot is for machine '{manifest.Machine.SystemName}' but target system is '{targetSystem.Name}'."));

        modulesByName = provider.GetSnapshotModules().ToDictionary(m => m.Name, StringComparer.Ordinal);

        foreach (var entry in manifest.Modules)
        {
            if (!modulesByName.TryGetValue(entry.Name, out var module))
            {
                if (entry.Required)
                    throw new SnapshotIncompatibleException(SnapshotCompatibility.Incompatible(
                        $"Snapshot requires unknown module '{entry.Name}'."));
                continue; // Unknown optional module handled (warned) during restore.
            }

            if (entry.Version > module.Version)
                throw new SnapshotIncompatibleException(SnapshotCompatibility.Incompatible(
                    $"Module '{entry.Name}' snapshot version {entry.Version} is newer than supported version {module.Version}."));
        }

        var compatibility = provider.ValidateSnapshot(manifest);
        if (!compatibility.IsCompatible)
            throw new SnapshotIncompatibleException(compatibility);
    }
}
