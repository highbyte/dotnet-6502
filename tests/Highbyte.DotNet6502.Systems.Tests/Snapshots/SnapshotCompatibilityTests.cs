using System.IO.Compression;
using System.Text.Json;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class SnapshotCompatibilityTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static GenericComputer BuildComputer()
        => new GenericComputerBuilder(new NullLoggerFactory()).WithCPU().WithMemory(1024 * 64).Build();

    /// <summary>Reads a real saved package into a manifest + raw entry bytes for repackaging.</summary>
    private static (SnapshotManifest manifest, Dictionary<string, byte[]> entries) ReadPackage(Stream package)
    {
        package.Position = 0;
        using var archive = new ZipArchive(package, ZipArchiveMode.Read);
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        SnapshotManifest? manifest = null;
        foreach (var entry in archive.Entries)
        {
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var bytes = ms.ToArray();
            entries[entry.FullName] = bytes;
            if (entry.FullName == SnapshotService.ManifestEntryName)
                manifest = JsonSerializer.Deserialize<SnapshotManifest>(bytes, s_jsonOptions);
        }
        return (manifest!, entries);
    }

    /// <summary>Writes a package with the given manifest and module entry bytes.</summary>
    private static MemoryStream WritePackage(SnapshotManifest manifest, Dictionary<string, byte[]> entries)
    {
        var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry(SnapshotService.ManifestEntryName);
            using (var ms = manifestEntry.Open())
                JsonSerializer.Serialize(ms, manifest, s_jsonOptions);

            foreach (var (path, bytes) in entries)
            {
                if (path == SnapshotService.ManifestEntryName)
                    continue;
                var entry = archive.CreateEntry(path);
                using var es = entry.Open();
                es.Write(bytes, 0, bytes.Length);
            }
        }
        output.Position = 0;
        return output;
    }

    private static MemoryStream SaveValidPackage()
    {
        var source = BuildComputer();
        var stream = new MemoryStream();
        new SnapshotService().Save(source, stream);
        return stream;
    }

    [Fact]
    public void Restore_rejects_wrong_machine()
    {
        var (manifest, entries) = ReadPackage(SaveValidPackage());
        manifest.Machine.SystemName = "C64";

        using var package = WritePackage(manifest, entries);
        var ex = Assert.Throws<SnapshotIncompatibleException>(
            () => new SnapshotService().Restore(BuildComputer(), package));
        Assert.Contains("C64", ex.Message);
    }

    [Fact]
    public void Restore_rejects_unknown_required_module()
    {
        var (manifest, entries) = ReadPackage(SaveValidPackage());
        manifest.Modules.Add(new SnapshotModuleEntry
        {
            Name = "totally-unknown",
            Version = 1,
            Required = true,
            Path = "modules/totally-unknown.bin",
        });

        using var package = WritePackage(manifest, entries);
        var ex = Assert.Throws<SnapshotIncompatibleException>(
            () => new SnapshotService().Restore(BuildComputer(), package));
        Assert.Contains("totally-unknown", ex.Message);
    }

    [Fact]
    public void Restore_rejects_newer_module_version()
    {
        var (manifest, entries) = ReadPackage(SaveValidPackage());
        var cpuEntry = manifest.Modules.Single(m => m.Name == Cpu6502SnapshotModule.ModuleName);
        cpuEntry.Version = 999;

        using var package = WritePackage(manifest, entries);
        Assert.Throws<SnapshotIncompatibleException>(
            () => new SnapshotService().Restore(BuildComputer(), package));
    }

    [Fact]
    public void Restore_rejects_newer_format_version()
    {
        var (manifest, entries) = ReadPackage(SaveValidPackage());
        manifest.FormatVersion = 999;

        using var package = WritePackage(manifest, entries);
        Assert.Throws<SnapshotIncompatibleException>(
            () => new SnapshotService().Restore(BuildComputer(), package));
    }

    [Fact]
    public void Restore_ignores_unknown_optional_module_with_warning()
    {
        var (manifest, entries) = ReadPackage(SaveValidPackage());
        manifest.Modules.Add(new SnapshotModuleEntry
        {
            Name = "future-peripheral",
            Version = 1,
            Required = false,
            Path = "modules/future-peripheral.bin",
        });
        entries["modules/future-peripheral.bin"] = new byte[] { 0x01, 0x02, 0x03 };

        using var package = WritePackage(manifest, entries);
        var result = new SnapshotService().Restore(BuildComputer(), package);

        Assert.Contains(result.Warnings, w => w.Contains("future-peripheral"));
    }
}
