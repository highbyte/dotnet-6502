using System.Buffers.Binary;
using System.Text;

namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge.Crt;

public static class C64CrtParser
{
    private const int MinimumHeaderLength = 0x40;
    private const int ChipHeaderLength = 0x10;
    private const string C64Signature = "C64 CARTRIDGE   ";
    private const string ChipSignature = "CHIP";

    public static C64CrtImage Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Parse(data.AsSpan());
    }

    public static C64CrtImage Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < MinimumHeaderLength)
            throw new C64CrtImageException("CRT image is shorter than the 64-byte header.");

        if (!data[..16].SequenceEqual(Encoding.ASCII.GetBytes(C64Signature)))
            throw new C64CrtImageException("CRT image does not contain the C64 CARTRIDGE signature.");

        var headerLength = ReadUInt32(data, 0x10);
        if (headerLength < MinimumHeaderLength || headerLength > data.Length)
            throw new C64CrtImageException($"CRT header length {headerLength} is invalid for an image of {data.Length} bytes.");
        if (data[0x18] > 1 || data[0x19] > 1)
            throw new C64CrtImageException("CRT EXROM and GAME header values must be 0 or 1.");

        var header = new C64CrtHeader(
            headerLength,
            ReadUInt16(data, 0x14),
            ReadUInt16(data, 0x16),
            ExromHigh: data[0x18] != 0,
            GameHigh: data[0x19] != 0,
            Subtype: data[0x1A],
            Name: ReadName(data.Slice(0x20, 32)));

        var chips = new List<C64CrtChip>();
        var offset = checked((int)headerLength);
        while (offset < data.Length)
        {
            if (data.Length - offset < ChipHeaderLength)
                throw new C64CrtImageException($"CRT CHIP header at offset 0x{offset:X} is truncated.");

            var chipHeader = data.Slice(offset, ChipHeaderLength);
            if (!chipHeader[..4].SequenceEqual(Encoding.ASCII.GetBytes(ChipSignature)))
                throw new C64CrtImageException($"CRT packet at offset 0x{offset:X} does not contain the CHIP signature.");

            var packetLength = ReadUInt32(chipHeader, 4);
            if (packetLength < ChipHeaderLength)
                throw new C64CrtImageException($"CRT CHIP packet length {packetLength} at offset 0x{offset:X} is invalid.");

            var rawChipType = ReadUInt16(chipHeader, 8);
            if (!Enum.IsDefined(typeof(C64CrtChipType), rawChipType))
                throw new C64CrtImageException($"CRT CHIP type {rawChipType} at offset 0x{offset:X} is invalid.");

            var bank = ReadUInt16(chipHeader, 10);
            var loadAddress = ReadUInt16(chipHeader, 12);
            var imageSize = ReadUInt16(chipHeader, 14);
            var minimumPacketLength = ChipHeaderLength + imageSize;
            var remainingLength = data.Length - offset;
            var effectivePacketLength = packetLength;
            if (packetLength > remainingLength)
            {
                // Some CRT images in the wild have an overstated final CHIP packet length
                // while the CHIP payload-size field and actual remaining bytes are correct.
                // Accept only that exact final-packet case; malformed middle packets, truncated
                // payloads, and trailing garbage still fail.
                if (minimumPacketLength == remainingLength)
                    effectivePacketLength = (uint)minimumPacketLength;
                else
                    throw new C64CrtImageException($"CRT CHIP packet length {packetLength} at offset 0x{offset:X} is invalid.");
            }

            if (imageSize > effectivePacketLength - ChipHeaderLength)
                throw new C64CrtImageException($"CRT CHIP payload size {imageSize} exceeds its packet length at offset 0x{offset:X}.");
            if (loadAddress + (uint)imageSize > 0x10000)
                throw new C64CrtImageException($"CRT CHIP at offset 0x{offset:X} crosses the 64K address boundary.");

            var payloadStart = offset + ChipHeaderLength;
            chips.Add(new C64CrtChip(
                (C64CrtChipType)rawChipType,
                bank,
                loadAddress,
                data.Slice(payloadStart, imageSize).ToArray()));

            offset += checked((int)effectivePacketLength);
        }

        if (chips.Count == 0)
            throw new C64CrtImageException("CRT image contains no CHIP packets.");

        return new C64CrtImage(header, chips);
    }

    public static C64CrtImage Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Parse(buffer.ToArray());
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, sizeof(ushort)));

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, sizeof(uint)));

    private static string ReadName(ReadOnlySpan<byte> data)
    {
        var nullIndex = data.IndexOf((byte)0);
        if (nullIndex >= 0)
            data = data[..nullIndex];
        return Encoding.ASCII.GetString(data).Trim();
    }
}
