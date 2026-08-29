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

    public bool IsBasic => FileType == BasicFileType;
    public bool IsAutoRun => AutoRunFlag != 0;
}

/// <summary>
/// Parses the first file in an Oric byte-level tape image. This is the logical format used by
/// OSDK Bas2Tap and emulators, rather than a recording of cassette pulses.
/// </summary>
public static class OricTapParser
{
    public const byte SyncByte = 0x16;
    public const byte HeaderMarker = 0x24;
    public const int MinimumSyncByteCount = 3;
    private const int HeaderBytesAfterMarker = 9;

    /// <summary>Parses the first file in <paramref name="tapData"/>.</summary>
    /// <exception cref="InvalidDataException">The data is not a valid Oric tape file.</exception>
    public static OricTapFile Parse(byte[] tapData)
    {
        ArgumentNullException.ThrowIfNull(tapData);

        var markerOffset = 0;
        while (markerOffset < tapData.Length && tapData[markerOffset] == SyncByte)
            markerOffset++;

        if (markerOffset < MinimumSyncByteCount ||
            markerOffset >= tapData.Length ||
            tapData[markerOffset] != HeaderMarker)
        {
            throw new InvalidDataException(
                $"Not an Oric TAP file: expected at least {MinimumSyncByteCount} sync bytes followed by ${HeaderMarker:X2}.");
        }

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
        var fileNameEnd = Array.IndexOf(tapData, (byte)0, fileNameOffset);
        if (fileNameEnd < 0)
            throw new InvalidDataException("The Oric TAP filename is not terminated.");

        var payloadOffset = fileNameEnd + 1;
        var payloadLength = endAddress - startAddress + 1;
        if (payloadOffset + payloadLength > tapData.Length)
        {
            throw new InvalidDataException(
                $"The Oric TAP payload is truncated: expected {payloadLength} bytes, found {tapData.Length - payloadOffset}.");
        }

        var name = Encoding.ASCII.GetString(tapData, fileNameOffset, fileNameEnd - fileNameOffset);
        var payload = tapData.AsSpan(payloadOffset, payloadLength).ToArray();
        return new OricTapFile(name, fileType, autoRunFlag, startAddress, endAddress, payload);
    }

    private static ushort ReadBigEndianWord(byte[] data, int offset)
        => (ushort)((data[offset] << 8) | data[offset + 1]);
}
