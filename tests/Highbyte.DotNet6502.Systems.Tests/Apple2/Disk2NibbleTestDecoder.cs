using Highbyte.DotNet6502.Systems.Apple2.Disk2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Assert-flavoured wrapper over the production decoders in <see cref="Disk2NibbleCodec"/>, so a
/// test that means "this must decode" fails at the point of decoding rather than on a later
/// comparison. The decoding itself moved into production when write support needed it; this no
/// longer carries its own copy of the algorithm.
/// </summary>
internal static class Disk2NibbleTestDecoder
{
    /// <summary>Decodes 343 disk bytes back into a 256-byte sector, verifying the checksum.</summary>
    public static byte[] DecodeSector(ReadOnlySpan<byte> encoded)
    {
        Assert.Equal(Disk2NibbleCodec.EncodedDataSize, encoded.Length);
        var sector = new byte[Disk2NibbleCodec.SectorSize];
        Assert.True(Disk2NibbleCodec.TryDecodeSector(encoded, sector), "Encoded sector failed to decode.");
        return sector;
    }

    /// <summary>Decoded fields of one address field (volume, track, sector, checksum-verified).</summary>
    public static (byte Volume, byte Track, byte Sector) DecodeAddressField(ReadOnlySpan<byte> fourAndFour)
    {
        Assert.Equal(8, fourAndFour.Length);
        Assert.True(
            Disk2NibbleCodec.TryDecodeAddressField(fourAndFour, out var volume, out var track, out var sector),
            "Address field failed its checksum.");
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
