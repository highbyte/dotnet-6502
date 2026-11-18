using System.Text;

namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Writes fixed little-endian primitive values into a snapshot module's binary payload.
/// Multi-byte integers are little-endian; byte arrays and strings are length-prefixed with a
/// little-endian <see cref="int"/> count (-1 represents null). The matching reader is
/// <see cref="SnapshotModuleReader"/>; both must stay in lock-step.
/// </summary>
public sealed class SnapshotModuleWriter
{
    private readonly BinaryWriter _writer;

    // SnapshotService owns the stream. Checkpoint writers can share their BinaryWriter
    // so the common CPU fields go directly to the same digest stream.
    public SnapshotModuleWriter(Stream stream)
        : this(new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) { }

    internal SnapshotModuleWriter(BinaryWriter writer) => _writer = writer;

    public void WriteByte(byte value) => _writer.Write(value);
    public void WriteBool(bool value) => _writer.Write(value);
    public void WriteUInt16(ushort value) => _writer.Write(value);
    public void WriteInt32(int value) => _writer.Write(value);
    public void WriteUInt32(uint value) => _writer.Write(value);
    public void WriteUInt64(ulong value) => _writer.Write(value);

    /// <summary>Writes a length-prefixed byte array. Null is encoded as length -1.</summary>
    public void WriteBytes(byte[]? value)
    {
        if (value == null)
        {
            _writer.Write(-1);
            return;
        }
        _writer.Write(value.Length);
        _writer.Write(value);
    }

    /// <summary>Writes a length-prefixed UTF-8 string. Null is encoded as length -1.</summary>
    public void WriteString(string? value)
    {
        if (value == null)
        {
            _writer.Write(-1);
            return;
        }
        var bytes = Encoding.UTF8.GetBytes(value);
        _writer.Write(bytes.Length);
        _writer.Write(bytes);
    }
}
