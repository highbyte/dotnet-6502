namespace Highbyte.DotNet6502.Systems.Apple2.Disk2;

/// <summary>
/// GCR encoding primitives for the Disk II 16-sector format: the 6-and-2 data encoding and the
/// 4-and-4 address-field encoding, as written by DOS 3.3's RWTS and read back by it and by the
/// controller's boot ROM.
///
/// The Disk II can only reliably store bytes whose high bit is set and that contain no more than
/// one pair of consecutive zero bits, giving 64 usable "disk byte" values. 6-and-2 encoding maps
/// 256 data bytes onto 342 six-bit values (256 holding the high 6 bits of each byte, 86 holding
/// the low 2 bits of up to three bytes each, bit-reversed), XOR-chains consecutive values, and
/// translates each result through the 64-entry disk-byte table. 4-and-4 encoding stores one byte
/// as two, spreading its bits across the odd bit positions of the pair.
/// </summary>
public static class Disk2NibbleCodec
{
    /// <summary>Number of data bytes in a decoded sector.</summary>
    public const int SectorSize = 256;

    /// <summary>Encoded size of a sector's data: 342 six-bit values plus the checksum byte.</summary>
    public const int EncodedDataSize = 343;

    /// <summary>The 86 auxiliary values holding the low 2 bits of the sector's bytes.</summary>
    private const int AuxChunkSize = 86;

    /// <summary>
    /// The 6-bit-value → disk-byte write translate table. $D5 and $AA would be bitwise legal but
    /// are excluded so they stay unique to the address/data field prologs.
    /// </summary>
    public static ReadOnlySpan<byte> WriteTranslateTable => new byte[64]
    {
        0x96, 0x97, 0x9A, 0x9B, 0x9D, 0x9E, 0x9F, 0xA6,
        0xA7, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF, 0xB2, 0xB3,
        0xB4, 0xB5, 0xB6, 0xB7, 0xB9, 0xBA, 0xBB, 0xBC,
        0xBD, 0xBE, 0xBF, 0xCB, 0xCD, 0xCE, 0xCF, 0xD3,
        0xD6, 0xD7, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE,
        0xDF, 0xE5, 0xE6, 0xE7, 0xE9, 0xEA, 0xEB, 0xEC,
        0xED, 0xEE, 0xEF, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6,
        0xF7, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF,
    };

    /// <summary>Marks a byte that is not one of the 64 legal disk bytes.</summary>
    private const byte InvalidDiskByte = 0xFF;

    /// <summary>
    /// Inverse of <see cref="WriteTranslateTable"/>: disk byte → 6-bit value, with
    /// <see cref="InvalidDiskByte"/> for every byte the drive could never legally have written.
    /// </summary>
    private static readonly byte[] s_readTranslateTable = BuildReadTranslateTable();

    private static byte[] BuildReadTranslateTable()
    {
        var inverse = new byte[256];
        Array.Fill(inverse, InvalidDiskByte);
        var table = WriteTranslateTable;
        for (var i = 0; i < table.Length; i++)
            inverse[table[i]] = (byte)i;
        return inverse;
    }

    /// <summary>The low ("odd bits") byte of a 4-and-4 encoded value.</summary>
    public static byte To44Lo(byte value) => (byte)((value >> 1) | 0xAA);

    /// <summary>The high ("even bits") byte of a 4-and-4 encoded value.</summary>
    public static byte To44Hi(byte value) => (byte)(value | 0xAA);

    /// <summary>Decodes a 4-and-4 encoded byte pair.</summary>
    public static byte From44(byte lo, byte hi) => (byte)(((lo << 1) | 0x01) & hi);

    /// <summary>
    /// Inverse of <see cref="EncodeSector"/>: 343 disk bytes back to a 256-byte sector.
    ///
    /// <para>Returns false rather than throwing on anything malformed — an illegal disk byte or a
    /// checksum mismatch. That is the normal case, not an exceptional one: this runs over whatever
    /// the emulated machine actually wrote, which during a partial or interrupted sector write is
    /// legitimately garbage, and a half-written sector must be dropped rather than persisted.</para>
    /// </summary>
    public static bool TryDecodeSector(ReadOnlySpan<byte> encoded, Span<byte> sectorData)
    {
        if (encoded.Length < EncodedDataSize || sectorData.Length < SectorSize)
            return false;

        // Undo the XOR chain, recovering the 342 six-bit values in written order:
        // aux[85..0] first, then high6[0..255].
        Span<byte> values = stackalloc byte[EncodedDataSize - 1];
        byte previous = 0;
        for (var i = 0; i < values.Length; i++)
        {
            var sixBit = s_readTranslateTable[encoded[i]];
            if (sixBit == InvalidDiskByte)
                return false;
            values[i] = (byte)(sixBit ^ previous);
            previous = values[i];
        }

        var checksum = s_readTranslateTable[encoded[EncodedDataSize - 1]];
        if (checksum == InvalidDiskByte || checksum != previous)
            return false;

        for (var i = 0; i < SectorSize; i++)
        {
            var high6 = values[AuxChunkSize + i];
            // The encoder fills aux[85 - (i % 86)] and the stream is written aux[85] first, so
            // byte i's aux value sits at stream position i % 86.
            var aux = values[i % AuxChunkSize];
            var lowBitsReversed = (aux >> (2 * (i / AuxChunkSize))) & 0x03;
            var low2 = ((lowBitsReversed & 0x01) << 1) | ((lowBitsReversed & 0x02) >> 1);
            sectorData[i] = (byte)((high6 << 2) | low2);
        }
        return true;
    }

    /// <summary>
    /// Decodes an 8-byte 4-and-4 address field, verifying its checksum. False for a field that
    /// does not check out, for the same reason as <see cref="TryDecodeSector"/>.
    /// </summary>
    public static bool TryDecodeAddressField(
        ReadOnlySpan<byte> fourAndFour, out byte volume, out byte track, out byte sector)
    {
        volume = 0;
        track = 0;
        sector = 0;
        if (fourAndFour.Length < 8)
            return false;

        volume = From44(fourAndFour[0], fourAndFour[1]);
        track = From44(fourAndFour[2], fourAndFour[3]);
        sector = From44(fourAndFour[4], fourAndFour[5]);
        var checksum = From44(fourAndFour[6], fourAndFour[7]);
        return checksum == (byte)(volume ^ track ^ sector);
    }

    /// <summary>
    /// 6-and-2 encodes one 256-byte sector into 343 disk bytes (342 data values + checksum),
    /// XOR-chained and translated exactly as RWTS's write routine emits them: the 86 auxiliary
    /// low-bit values first, then the 256 high-bit values, then the running checksum.
    /// </summary>
    /// <param name="sectorData">The 256 data bytes.</param>
    /// <param name="encoded">Receives the 343 encoded disk bytes.</param>
    public static void EncodeSector(ReadOnlySpan<byte> sectorData, Span<byte> encoded)
    {
        if (sectorData.Length != SectorSize)
            throw new ArgumentException($"Sector data must be {SectorSize} bytes.", nameof(sectorData));
        if (encoded.Length < EncodedDataSize)
            throw new ArgumentException($"Output must hold {EncodedDataSize} bytes.", nameof(encoded));

        Span<byte> high6 = stackalloc byte[SectorSize];
        Span<byte> aux = stackalloc byte[AuxChunkSize];
        aux.Clear();

        // Byte i contributes its high 6 bits to high6[i] and its low 2 bits — reversed — to a
        // 2-bit group of aux[85 - (i % 86)], group selected by i / 86 (the third group only spans
        // data bytes $AC-$FF, so aux entries 0 and 1 have an empty top group).
        var twoShift = 0;
        for (int i = 0, auxPos = AuxChunkSize - 1; i < SectorSize; i++)
        {
            var value = sectorData[i];
            high6[i] = (byte)(value >> 2);
            aux[auxPos] |= (byte)((((value & 0x01) << 1) | ((value & 0x02) >> 1)) << twoShift);
            if (auxPos == 0)
            {
                auxPos = AuxChunkSize;
                twoShift += 2;
            }
            auxPos--;
        }

        var translate = WriteTranslateTable;
        var outPos = 0;
        byte previous = 0;
        for (var i = AuxChunkSize - 1; i >= 0; i--)
        {
            encoded[outPos++] = translate[aux[i] ^ previous];
            previous = aux[i];
        }
        for (var i = 0; i < SectorSize; i++)
        {
            encoded[outPos++] = translate[high6[i] ^ previous];
            previous = high6[i];
        }
        encoded[outPos] = translate[previous];
    }
}
