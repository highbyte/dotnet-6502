using Highbyte.DotNet6502.Systems.Apple2.Disk2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Disk2NibbleCodecTests
{
    [Fact]
    public void WriteTranslateTable_Has_64_Unique_Valid_Disk_Bytes()
    {
        var table = Disk2NibbleCodec.WriteTranslateTable.ToArray();

        Assert.Equal(64, table.Length);
        Assert.Equal(64, table.Distinct().Count());
        Assert.All(table, b => Assert.True((b & 0x80) != 0, $"Disk byte ${b:X2} must have the high bit set."));

        // $D5 and $AA are reserved for the field prologs.
        Assert.DoesNotContain((byte)0xD5, table);
        Assert.DoesNotContain((byte)0xAA, table);
    }

    [Fact]
    public void FourAndFour_Round_Trips_All_Byte_Values()
    {
        for (var value = 0; value <= 255; value++)
        {
            var lo = Disk2NibbleCodec.To44Lo((byte)value);
            var hi = Disk2NibbleCodec.To44Hi((byte)value);

            // Both halves carry their bits interleaved with ones, so they are valid disk bytes.
            Assert.True((lo & 0xAA) == 0xAA);
            Assert.True((hi & 0xAA) == 0xAA);

            Assert.Equal((byte)value, Disk2NibbleCodec.From44(lo, hi));
        }
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0xFF)]
    [InlineData(0xA5)]
    public void EncodeSector_Round_Trips_A_Constant_Sector(byte fill)
    {
        var sector = new byte[Disk2NibbleCodec.SectorSize];
        Array.Fill(sector, fill);

        Assert.Equal(sector, EncodeThenDecode(sector));
    }

    [Fact]
    public void EncodeSector_Round_Trips_A_Ramp_Sector()
    {
        var sector = new byte[Disk2NibbleCodec.SectorSize];
        for (var i = 0; i < sector.Length; i++)
            sector[i] = (byte)i;

        Assert.Equal(sector, EncodeThenDecode(sector));
    }

    [Fact]
    public void EncodeSector_Round_Trips_Pseudo_Random_Sectors()
    {
        var random = new Random(6502);
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var sector = new byte[Disk2NibbleCodec.SectorSize];
            random.NextBytes(sector);

            Assert.Equal(sector, EncodeThenDecode(sector));
        }
    }

    [Fact]
    public void EncodeSector_Emits_Only_Valid_Disk_Bytes()
    {
        var random = new Random(1541);
        var sector = new byte[Disk2NibbleCodec.SectorSize];
        random.NextBytes(sector);

        var encoded = new byte[Disk2NibbleCodec.EncodedDataSize];
        Disk2NibbleCodec.EncodeSector(sector, encoded);

        var validBytes = Disk2NibbleCodec.WriteTranslateTable.ToArray().ToHashSet();
        Assert.All(encoded, b => Assert.Contains(b, validBytes));
    }

    [Fact]
    public void EncodeSector_Rejects_Wrong_Sizes()
    {
        Assert.Throws<ArgumentException>(
            () => Disk2NibbleCodec.EncodeSector(new byte[255], new byte[Disk2NibbleCodec.EncodedDataSize]));
        Assert.Throws<ArgumentException>(
            () => Disk2NibbleCodec.EncodeSector(new byte[Disk2NibbleCodec.SectorSize], new byte[342]));
    }

    private static byte[] EncodeThenDecode(byte[] sector)
    {
        var encoded = new byte[Disk2NibbleCodec.EncodedDataSize];
        Disk2NibbleCodec.EncodeSector(sector, encoded);
        return Disk2NibbleTestDecoder.DecodeSector(encoded);
    }
}
