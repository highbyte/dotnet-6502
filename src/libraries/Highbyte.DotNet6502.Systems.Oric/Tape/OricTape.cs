namespace Highbyte.DotNet6502.Systems.Oric.Tape;

/// <summary>
/// A byte-level Oric tape transport. The position advances as the Atmos ROM asks for bytes, so a
/// multi-file image can service later <c>CLOAD</c> operations without being reinserted.
/// </summary>
public sealed class OricTape
{
    private byte[]? _data;

    public bool IsInserted => _data is not null;
    public bool IsAtEnd => _data is not null && Position >= _data.Length;
    public int Position { get; private set; }
    public int Length => _data?.Length ?? 0;
    public IReadOnlyList<OricTapFile> Files { get; private set; } = Array.Empty<OricTapFile>();

    /// <summary>Inserts and rewinds a validated byte-level TAP image.</summary>
    public IReadOnlyList<OricTapFile> Insert(byte[] tapData)
    {
        ArgumentNullException.ThrowIfNull(tapData);

        var files = OricTapParser.ParseAll(tapData);
        _data = tapData.ToArray();
        Files = files;
        Position = 0;
        return Files;
    }

    public void Eject()
    {
        _data = null;
        Files = Array.Empty<OricTapFile>();
        Position = 0;
    }

    public void Rewind() => Position = 0;

    internal bool SeekToNextSyncByte()
    {
        if (_data is null)
            return false;

        while (Position < _data.Length && _data[Position] != OricTapParser.SyncByte)
            Position++;

        return Position < _data.Length;
    }

    internal bool TryReadByte(out byte value)
    {
        if (_data is null || Position >= _data.Length)
        {
            value = 0;
            return false;
        }

        value = _data[Position++];
        return true;
    }
}
