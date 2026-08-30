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
    public string? SourceName { get; private set; }
    public IReadOnlyList<OricTapFile> Files { get; private set; } = Array.Empty<OricTapFile>();
    public IReadOnlyList<OricTapRecord> Records { get; private set; } = Array.Empty<OricTapRecord>();

    /// <summary>The record containing the current byte position, or -1 between records.</summary>
    public int CurrentRecordIndex
    {
        get
        {
            for (var index = 0; index < Records.Count; index++)
            {
                var record = Records[index];
                if (Position >= record.StartOffset && Position < record.EndOffset)
                    return index;
            }
            return -1;
        }
    }

    public OricTapFile? CurrentFile
    {
        get
        {
            var index = CurrentRecordIndex;
            return index >= 0 ? Records[index].File : null;
        }
    }

    /// <summary>The next record after, or at, the current tape location; -1 at tape end.</summary>
    public int NextRecordIndex => GetNextRecordIndex();

    public bool CanSeekToPreviousRecord => GetPreviousRecordIndex() >= 0;
    public bool CanSeekToNextRecord => NextRecordIndex >= 0;

    /// <summary>Inserts and rewinds a validated byte-level TAP image.</summary>
    public IReadOnlyList<OricTapFile> Insert(byte[] tapData, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(tapData);

        var records = OricTapParser.ParseRecords(tapData);
        _data = tapData.ToArray();
        Records = records;
        Files = records.Select(record => record.File).ToArray();
        SourceName = string.IsNullOrWhiteSpace(sourceName) ? null : sourceName;
        Position = 0;
        return Files;
    }

    public void Eject()
    {
        _data = null;
        Files = Array.Empty<OricTapFile>();
        Records = Array.Empty<OricTapRecord>();
        SourceName = null;
        Position = 0;
    }

    public void Rewind() => Position = 0;

    /// <summary>Moves to the parsed record preceding the current tape location.</summary>
    public bool SeekToPreviousRecord()
    {
        var index = GetPreviousRecordIndex();
        if (index < 0)
            return false;

        Position = Records[index].StartOffset;
        return true;
    }

    /// <summary>Moves to the parsed record following the current tape location.</summary>
    public bool SeekToNextRecord()
    {
        var index = GetNextRecordIndex();
        if (index < 0)
            return false;

        Position = Records[index].StartOffset;
        return true;
    }

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

    private int GetPreviousRecordIndex()
    {
        var currentIndex = CurrentRecordIndex;
        if (currentIndex >= 0)
            return currentIndex - 1;

        for (var index = Records.Count - 1; index >= 0; index--)
        {
            if (Records[index].EndOffset <= Position)
                return index;
        }
        return -1;
    }

    private int GetNextRecordIndex()
    {
        var currentIndex = CurrentRecordIndex;
        if (currentIndex >= 0)
            return currentIndex + 1 < Records.Count ? currentIndex + 1 : -1;

        for (var index = 0; index < Records.Count; index++)
        {
            if (Records[index].StartOffset >= Position)
                return index;
        }
        return -1;
    }
}
