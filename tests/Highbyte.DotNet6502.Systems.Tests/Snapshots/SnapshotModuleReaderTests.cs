using System.Buffers.Binary;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class SnapshotModuleReaderTests
{
    [Theory]
    [InlineData(-2)]
    [InlineData(int.MaxValue)]
    [InlineData(8)]
    public void Snapshot_rejects_invalid_excessive_and_truncated_length_prefixed_data(int length)
    {
        var bytes = new byte[6];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, length);
        using var stream = new MemoryStream(bytes);
        var reader = new SnapshotModuleReader(stream);
        Assert.Throws<SnapshotException>(() => reader.ReadBytes());
        stream.Position = 0;
        Assert.Throws<SnapshotException>(() => reader.ReadString());
    }
}
