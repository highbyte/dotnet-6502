using System.Text;

namespace Highbyte.DotNet6502.Systems.Oric.Tape;

/// <summary>A file stored in an Oric byte-level <c>.tap</c> image.</summary>
public sealed record OricTapFile(
    string Name,
    byte FileType,
    byte AutoRunFlag,
    ushort StartAddress,
    ushort EndAddress,
    byte[] Data)
{
    public const byte BasicFileType = 0x00;
    public const byte MachineCodeFileType = 0x80;

    public bool IsBasic => FileType == BasicFileType;
    public bool IsMachineCode => FileType == MachineCodeFileType;
    public bool IsAutoRun => AutoRunFlag != 0;
}

/// <summary>
/// Parses files in an Oric byte-level tape image. This is the logical format used by OSDK and
/// emulators, rather than a recording of cassette pulses.
/// </summary>
public static class OricTapParser
{
    public const byte SyncByte = 0x16;
    public const byte HeaderMarker = 0x24;
    public const int MinimumSyncByteCount = 3;
    public const int MaximumFileNameLength = 16;
    private const int HeaderBytesAfterMarker = 9;

    /// <summary>Parses the first file in <paramref name="tapData"/>.</summary>
    /// <exception cref="InvalidDataException">The data is not a valid Oric tape file.</exception>
    public static OricTapFile Parse(byte[] tapData)
    {
        ArgumentNullException.ThrowIfNull(tapData);

        var markerOffset = FindMarker(tapData, 0, requireLeaderAtSearchOffset: true);
        if (markerOffset < 0)
        {
            throw new InvalidDataException(
                $"Not an Oric TAP file: expected at least {MinimumSyncByteCount} sync bytes followed by ${HeaderMarker:X2}.");
        }

        return ParseFile(tapData, markerOffset, out _);
    }

    /// <summary>
    /// Parses every file in <paramref name="tapData"/>. Bytes between records are treated as
    /// tape leader or padding and skipped when locating the next standard sync/header sequence.
    /// </summary>
    /// <exception cref="InvalidDataException">The data is not a valid Oric tape image.</exception>
    public static IReadOnlyList<OricTapFile> ParseAll(byte[] tapData)
    {
        ArgumentNullException.ThrowIfNull(tapData);

        var firstMarkerOffset = FindMarker(tapData, 0, requireLeaderAtSearchOffset: true);
        if (firstMarkerOffset < 0)
        {
            throw new InvalidDataException(
                $"Not an Oric TAP file: expected at least {MinimumSyncByteCount} sync bytes followed by ${HeaderMarker:X2}.");
        }

        var files = new List<OricTapFile>();
        var markerOffset = firstMarkerOffset;
        while (markerOffset >= 0)
        {
            files.Add(ParseFile(tapData, markerOffset, out var nextOffset));
            markerOffset = FindMarker(tapData, nextOffset, requireLeaderAtSearchOffset: false);
        }

        return files.ToArray();
    }

    private static OricTapFile ParseFile(byte[] tapData, int markerOffset, out int nextOffset)
    {
        var headerOffset = markerOffset + 1;
        if (headerOffset + HeaderBytesAfterMarker > tapData.Length)
            throw new InvalidDataException("The Oric TAP header is truncated.");

        var fileType = tapData[headerOffset + 2];
        var autoRunFlag = tapData[headerOffset + 3];
        var endAddress = ReadBigEndianWord(tapData, headerOffset + 4);
        var startAddress = ReadBigEndianWord(tapData, headerOffset + 6);
        if (endAddress < startAddress)
        {
            throw new InvalidDataException(
                $"The Oric TAP address range is invalid: ${startAddress:X4}-${endAddress:X4}.");
        }

        var fileNameOffset = headerOffset + HeaderBytesAfterMarker;
        var availableFileNameBytes = Math.Min(MaximumFileNameLength + 1, tapData.Length - fileNameOffset);
        var fileNameEnd = Array.IndexOf(tapData, (byte)0, fileNameOffset, availableFileNameBytes);
        if (fileNameEnd < 0)
        {
            throw new InvalidDataException(
                $"The Oric TAP filename is not terminated within {MaximumFileNameLength} characters.");
        }

        var payloadOffset = fileNameEnd + 1;
        var payloadLength = endAddress - startAddress + 1;
        if (payloadOffset + payloadLength > tapData.Length)
        {
            throw new InvalidDataException(
                $"The Oric TAP payload is truncated: expected {payloadLength} bytes, found {tapData.Length - payloadOffset}.");
        }

        var name = Encoding.ASCII.GetString(tapData, fileNameOffset, fileNameEnd - fileNameOffset);
        var payload = tapData.AsSpan(payloadOffset, payloadLength).ToArray();
        nextOffset = payloadOffset + payloadLength;
        return new OricTapFile(name, fileType, autoRunFlag, startAddress, endAddress, payload);
    }

    private static int FindMarker(byte[] tapData, int searchOffset, bool requireLeaderAtSearchOffset)
    {
        var offset = searchOffset;
        while (offset < tapData.Length)
        {
            if (tapData[offset] != SyncByte)
            {
                if (requireLeaderAtSearchOffset)
                    return -1;
                offset++;
                continue;
            }

            var leaderOffset = offset;
            while (offset < tapData.Length && tapData[offset] == SyncByte)
                offset++;

            if (offset - leaderOffset >= MinimumSyncByteCount &&
                offset < tapData.Length &&
                tapData[offset] == HeaderMarker)
            {
                return offset;
            }

            if (requireLeaderAtSearchOffset)
                return -1;
        }

        return -1;
    }

    private static ushort ReadBigEndianWord(byte[] data, int offset)
        => (ushort)((data[offset] << 8) | data[offset + 1]);
}
