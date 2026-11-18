using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Highbyte.DotNet6502.Systems.Recording;

/// <summary>Validates a complete package before playback. Never extracts archive paths to disk.</summary>
public sealed class DemoReader : IDisposable
{
    public const long MaxSnapshotBytes = 32 * 1024 * 1024;
    public const long MaxPackageBytes = 128 * 1024 * 1024;
    private readonly ZipArchive _archive;
    public DemoManifest Manifest { get; }

    public DemoReader(Stream input)
    {
        if (!input.CanSeek || input.Length > MaxPackageBytes)
            throw new InvalidDataException("Demo must be seekable and no larger than 128 MiB.");
        _archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        try
        {
            if (_archive.Entries.Count != 3 || _archive.Entries.Select(e => e.FullName).Distinct(StringComparer.Ordinal).Count() != 3
                || _archive.Entries.Any(e => e.FullName is not (DemoWriter.ManifestName or DemoWriter.SnapshotName or DemoWriter.TimelineName)))
                throw new InvalidDataException("Demo contains missing, duplicate, or unexpected entries.");
            if (_archive.Entries.Any(e => e.Length > MaxPackageBytes) || _archive.Entries.Sum(e => e.Length) > MaxPackageBytes)
                throw new InvalidDataException("Demo expanded size exceeds 128 MiB.");
            var manifestEntry = Entry(DemoWriter.ManifestName);
            if (manifestEntry.Length > 16384)
                throw new InvalidDataException("Demo manifest is too large.");
            using (var manifestStream = OpenEntry(manifestEntry))
                Manifest = JsonSerializer.Deserialize(manifestStream, DemoJsonContext.Default.DemoManifest)
                    ?? throw new InvalidDataException("Demo manifest is missing.");
            if (Manifest.FormatVersion != 1 || Manifest.TotalFrames < 0 || string.IsNullOrEmpty(Manifest.SystemName)
                || string.IsNullOrEmpty(Manifest.Schema) || Manifest.SchemaVersion < 1
                || string.IsNullOrEmpty(Manifest.CompatibilityId) || !double.IsFinite(Manifest.FramesPerSecond)
                || Manifest.FramesPerSecond <= 0 || !IsDigest(Manifest.InitialStateDigest))
                throw new InvalidDataException("Demo manifest is invalid or its format version is unsupported.");
            VerifyHash(DemoWriter.SnapshotName, Manifest.SnapshotSha256);
            VerifyHash(DemoWriter.TimelineName, Manifest.TimelineSha256);
            ValidateSnapshot();
        }
        catch
        {
            _archive.Dispose();
            throw;
        }
    }

    private ZipArchiveEntry Entry(string name) => _archive.GetEntry(name)
        ?? throw new InvalidDataException($"Missing demo entry '{name}'.");
    private static Stream OpenEntry(ZipArchiveEntry entry) => new DemoLimitedReadStream(entry.Open(), entry.Length);

    private static bool IsDigest(string? digest) => digest is { Length: 64 } && digest.All(char.IsAsciiHexDigit);

    private void VerifyHash(string name, string expected)
    {
        using var stream = OpenEntry(Entry(name));
        if (!IsDigest(expected) || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(stream)), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Demo entry '{name}' failed its integrity check.");
    }

    private void ValidateSnapshot()
    {
        using var snapshot = OpenSnapshot();
        using var archive = new ZipArchive(snapshot, ZipArchiveMode.Read);
        if (archive.Entries.Count > 128 || archive.Entries.Sum(e => e.Length) > MaxSnapshotBytes
            || archive.Entries.Select(e => e.FullName).Distinct(StringComparer.Ordinal).Count() != archive.Entries.Count
            || archive.Entries.Any(e => e.Length > MaxSnapshotBytes || e.FullName.StartsWith('/')
                || e.FullName.Contains('\\') || e.FullName.Split('/').Any(part => part is ".." or ".")))
            throw new InvalidDataException("Demo snapshot has unsafe entries or exceeds its size limit.");
        foreach (var entry in archive.Entries)
        {
            using var payload = OpenEntry(entry);
            payload.CopyTo(Stream.Null);
        }
    }

    public MemoryStream OpenSnapshot()
    {
        var entry = Entry(DemoWriter.SnapshotName);
        if (entry.Length > MaxSnapshotBytes)
            throw new InvalidDataException("Demo snapshot exceeds 32 MiB.");
        var buffer = new MemoryStream((int)entry.Length);
        using (var source = OpenEntry(entry))
            source.CopyTo(buffer);
        buffer.Position = 0;
        return buffer;
    }

    public void ValidateAdapter(IRecordingAdapter adapter)
    {
        if (Manifest.SystemName != adapter.SystemName || Manifest.Schema != adapter.Schema
            || Manifest.SchemaVersion != adapter.SchemaVersion || Manifest.CompatibilityId != adapter.CompatibilityId
            || Manifest.FramesPerSecond != adapter.FramesPerSecond)
            throw new InvalidDataException("Demo requires a different machine, input schema, ROMs, configuration, or emulator build.");
        using var cursor = OpenTimeline();
        long frames = 0;
        long lastCheckpoint = -1;
        while (cursor.Read() is { } record)
        {
            if (record.Input != null)
            {
                adapter.ValidateInput(record.Input);
                if (record.Duration > 1 && !adapter.CanRepeatInput(record.Input))
                    throw new InvalidDataException("An event-bearing input record cannot repeat.");
                frames = checked(frames + record.Duration);
                if (frames > Manifest.TotalFrames)
                    throw new InvalidDataException("Demo input exceeds its declared frame count.");
            }
            else
            {
                if (record.Frame != frames || record.Frame < lastCheckpoint)
                    throw new InvalidDataException("Demo checkpoint is out of order.");
                lastCheckpoint = record.Frame;
            }
        }
        if (frames != Manifest.TotalFrames || lastCheckpoint != frames)
            throw new InvalidDataException("Demo timeline is incomplete or has no final checkpoint.");
    }

    public DemoTimelineCursor OpenTimeline() => new(OpenEntry(Entry(DemoWriter.TimelineName)));
    public void Dispose() => _archive.Dispose();
}

public sealed record DemoTimelineRecord(byte[]? Input, uint Duration, long Frame, byte[]? Digest);

/// <summary>Sequential cursor; at most one bounded record is retained.</summary>
public sealed class DemoTimelineCursor(Stream stream) : IDisposable
{
    private readonly BinaryReader _reader = new(stream);

    public DemoTimelineRecord? Read()
    {
        int tag = _reader.BaseStream.ReadByte();
        if (tag == -1)
            return null;
        if (tag == 1)
        {
            var frame = _reader.ReadInt64();
            var digest = _reader.ReadBytes(32);
            if (frame < 0 || digest.Length != 32)
                throw new InvalidDataException("Truncated or invalid demo checkpoint.");
            return new(null, 0, frame, digest);
        }
        if (tag != 0)
            throw new InvalidDataException("Unknown demo timeline record.");
        uint duration = _reader.ReadUInt32();
        int count = _reader.ReadInt32();
        if (duration == 0 || count < 0 || count > DemoWriter.MaxInputBytes)
            throw new InvalidDataException("Invalid demo input duration or size.");
        var input = _reader.ReadBytes(count);
        if (input.Length != count)
            throw new InvalidDataException("Truncated demo input record.");
        return new(input, duration, 0, null);
    }

    public void Dispose() => _reader.Dispose();
}
