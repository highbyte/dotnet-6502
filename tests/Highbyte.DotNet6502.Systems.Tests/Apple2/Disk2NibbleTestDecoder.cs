using Highbyte.DotNet6502.Systems.Apple2.Disk2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Test-side inverse of the production 6-and-2/4-and-4 encoders — the same job RWTS's read
/// routines do on a real machine. Kept in the test project because the read-only emulation has
/// no production decode path.
/// </summary>
internal static class Disk2NibbleTestDecoder
{
    private const int AuxChunkSize = 86;

    private static readonly byte[] s_inverseTranslate = BuildInverseTranslate();

    private static byte[] BuildInverseTranslate()
    {
        var inverse = new byte[256];
        Array.Fill(inverse, (byte)0xFF);
        var table = Disk2NibbleCodec.WriteTranslateTable;
        for (var i = 0; i < table.Length; i++)
            inverse[table[i]] = (byte)i;
        return inverse;
    }

    /// <summary>Decodes 343 disk bytes back into a 256-byte sector, verifying the checksum.</summary>
    public static byte[] DecodeSector(ReadOnlySpan<byte> encoded)
    {
        Assert.Equal(Disk2NibbleCodec.EncodedDataSize, encoded.Length);

        // Undo the XOR chain, recovering the 342 six-bit values in written order:
        // aux[85..0] first, then high6[0..255].
        var values = new byte[342];
        byte previous = 0;
        for (var i = 0; i < values.Length; i++)
        {
            var sixBit = s_inverseTranslate[encoded[i]];
            Assert.NotEqual((byte)0xFF, sixBit);
            values[i] = (byte)(sixBit ^ previous);
            previous = values[i];
        }
        Assert.Equal(previous, s_inverseTranslate[encoded[342]]);   // trailing checksum

        var sector = new byte[Disk2NibbleCodec.SectorSize];
        for (var i = 0; i < Disk2NibbleCodec.SectorSize; i++)
        {
            var high6 = values[AuxChunkSize + i];
            // The encoder fills aux[85 - (i % 86)] and the stream is written aux[85] first, so
            // byte i's aux value sits at stream position i % 86.
            var aux = values[i % AuxChunkSize];
            var lowBitsReversed = (aux >> (2 * (i / AuxChunkSize))) & 0x03;
            var low2 = ((lowBitsReversed & 0x01) << 1) | ((lowBitsReversed & 0x02) >> 1);
            sector[i] = (byte)((high6 << 2) | low2);
        }
        return sector;
    }

    /// <summary>Decoded fields of one address field (volume, track, sector, checksum-verified).</summary>
    public static (byte Volume, byte Track, byte Sector) DecodeAddressField(ReadOnlySpan<byte> fourAndFour)
    {
        Assert.Equal(8, fourAndFour.Length);
        var volume = Disk2NibbleCodec.From44(fourAndFour[0], fourAndFour[1]);
        var track = Disk2NibbleCodec.From44(fourAndFour[2], fourAndFour[3]);
        var sector = Disk2NibbleCodec.From44(fourAndFour[4], fourAndFour[5]);
        var checksum = Disk2NibbleCodec.From44(fourAndFour[6], fourAndFour[7]);
        Assert.Equal((byte)(volume ^ track ^ sector), checksum);
        return (volume, track, sector);
    }

    /// <summary>Index just past the next occurrence of <paramref name="pattern"/>, or -1.</summary>
    public static int FindAfter(ReadOnlySpan<byte> data, int start, ReadOnlySpan<byte> pattern)
    {
        for (var i = start; i <= data.Length - pattern.Length; i++)
        {
            if (data.Slice(i, pattern.Length).SequenceEqual(pattern))
                return i + pattern.Length;
        }
        return -1;
    }
}
