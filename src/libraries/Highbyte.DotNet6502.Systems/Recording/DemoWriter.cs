using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Highbyte.DotNet6502.Systems.Recording;

/// <summary>
/// Streams a demo to caller-owned storage. Only the current input run is buffered. The manifest
/// is written on completion; disposal after a failure never turns an interrupted demo into a
/// complete recording. A host must publish its temporary file only after Complete succeeds.
/// </summary>
public sealed class DemoWriter : IDisposable
{
    public const string FileExtension = ".d6502demo";
    internal const string ManifestName = "demo.json";
    internal const string SnapshotName = "initial.d6502snap";
    internal const string TimelineName = "timeline.bin";
    internal const int MaxInputBytes = 4096;
    private readonly ZipArchive _archive;
    private readonly BinaryWriter _timeline;
    private readonly IncrementalHash _timelineHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly DemoManifest _manifest;
    private byte[]? _pendingInput;
    private uint _duration;
    private bool _completed;
    private bool _disposed;
    private long _timelineBytes;

    public long Frames { get; private set; }

    public DemoWriter(Stream output, Stream initialSnapshot, IRecordingAdapter adapter,
        string? configurationVariant = null)
    {
        adapter.ValidateSession();
        if (!initialSnapshot.CanSeek || initialSnapshot.Length > DemoReader.MaxSnapshotBytes)
            throw new InvalidDataException("The initial snapshot exceeds the demo storage limit or is not seekable.");
        _manifest = new DemoManifest
        {
            SystemName = adapter.SystemName,
            Schema = adapter.Schema,
            SchemaVersion = adapter.SchemaVersion,
            CompatibilityId = adapter.CompatibilityId,
            ConfigurationVariant = configurationVariant,
            FramesPerSecond = adapter.FramesPerSecond,
            InitialStateDigest = Convert.ToHexStringLower(adapter.ComputeStateDigest()),
        };
        initialSnapshot.Position = 0;
        _manifest.SnapshotSha256 = Convert.ToHexStringLower(SHA256.HashData(initialSnapshot));
        initialSnapshot.Position = 0;
        _archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        using (var snapshot = _archive.CreateEntry(SnapshotName).Open())
            initialSnapshot.CopyTo(snapshot);
        _timeline = new BinaryWriter(_archive.CreateEntry(TimelineName).Open(), Encoding.UTF8);
    }

    public void AppendInput(byte[] input, bool canRepeat)
    {
        ObjectDisposedException.ThrowIf(_disposed || _completed, this);
        if (input.Length > MaxInputBytes)
            throw new InvalidDataException("Machine input exceeds the demo record limit.");
        if (canRepeat && _duration < uint.MaxValue && _pendingInput != null && input.AsSpan().SequenceEqual(_pendingInput))
            _duration++;
        else
        {
            FlushRun();
            _pendingInput = input.ToArray();
            _duration = 1;
        }
        Frames = checked(Frames + 1);
        // Event-bearing payloads must never be coalesced into a repeating state run.
        if (!canRepeat)
            FlushRun();
    }

    public void WriteCheckpoint(byte[] digest)
    {
        ObjectDisposedException.ThrowIf(_disposed || _completed, this);
        if (digest.Length != 32)
            throw new InvalidDataException("State digests must be SHA-256 values.");
        FlushRun();
        WriteRecord(writer =>
        {
            writer.Write((byte)1);
            writer.Write(Frames);
            writer.Write(digest);
        });
    }

    public void Complete(byte[] finalDigest)
    {
        WriteCheckpoint(finalDigest);
        _timeline.Dispose();
        _manifest.TotalFrames = Frames;
        _manifest.TimelineSha256 = Convert.ToHexStringLower(_timelineHash.GetHashAndReset());
        using (var manifest = _archive.CreateEntry(ManifestName).Open())
            JsonSerializer.Serialize(manifest, _manifest, DemoJsonContext.Default.DemoManifest);
        _archive.Dispose();
        _completed = true;
    }

    private void FlushRun()
    {
        if (_pendingInput == null)
            return;
        WriteRecord(writer =>
        {
            writer.Write((byte)0);
            writer.Write(_duration);
            writer.Write(_pendingInput.Length);
            writer.Write(_pendingInput);
        });
        _pendingInput = null;
        _duration = 0;
    }

    private void WriteRecord(Action<BinaryWriter> write)
    {
        using var buffer = new MemoryStream(MaxInputBytes + 64);
        using var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true);
        write(writer);
        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        if (_timelineBytes + bytes.Length > DemoReader.MaxPackageBytes - DemoReader.MaxSnapshotBytes - 16384)
            throw new IOException("Recording reached the demo storage limit. This partial demo cannot be saved as complete.");
        _timeline.Write(bytes);
        _timelineHash.AppendData(bytes);
        _timelineBytes += bytes.Length;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _timeline.Dispose();
        _archive.Dispose();
        _timelineHash.Dispose();
    }
}
