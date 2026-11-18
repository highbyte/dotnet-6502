using System.Text;

namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Reads the fixed little-endian primitive values written by <see cref="SnapshotModuleWriter"/>.
/// Reads must occur in the exact order they were written.
/// </summary>
public sealed class SnapshotModuleReader
{
    private readonly BinaryReader _reader;

    public SnapshotModuleReader(Stream stream)
    {
        _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
    }

    public byte ReadByte() => _reader.ReadByte();
    public bool ReadBool() => _reader.ReadBoolean();
    public ushort ReadUInt16() => _reader.ReadUInt16();
    public int ReadInt32() => _reader.ReadInt32();
    public uint ReadUInt32() => _reader.ReadUInt32();
    public ulong ReadUInt64() => _reader.ReadUInt64();

    /// <summary>Reads a length-prefixed byte array, or null if the stored length was -1.</summary>
    public byte[]? ReadBytes()
    {
        int length = _reader.ReadInt32();
        if (length == -1)
            return null;
        return ReadExactPayload(length);
    }

    /// <summary>Reads a length-prefixed UTF-8 string, or null if the stored length was -1.</summary>
    public string? ReadString()
    {
        int length = _reader.ReadInt32();
        if (length == -1)
            return null;
        var bytes = ReadExactPayload(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private byte[] ReadExactPayload(int length)
    {
        var stream = _reader.BaseStream;
        if (length < 0 || length > 64 * 1024 * 1024
            || (stream.CanSeek && length > stream.Length - stream.Position))
            throw new SnapshotException("Snapshot module contains an invalid or excessive payload length.");
        var bytes = _reader.ReadBytes(length);
        if (bytes.Length != length)
            throw new SnapshotException("Snapshot module payload is truncated.");
        return bytes;
    }
}
