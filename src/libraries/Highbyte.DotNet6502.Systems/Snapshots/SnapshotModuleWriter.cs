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

    public SnapshotModuleWriter(Stream stream)
    {
        // BinaryWriter writes integers little-endian on all platforms. Leave the underlying
        // stream open so the caller (SnapshotService) controls its lifetime.
        _writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
    }

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
