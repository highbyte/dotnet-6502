using Highbyte.DotNet6502.Systems.Apple2.DiskImage;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class DskParserTests
{
    /// <summary>
    /// Builds a synthetic DOS 3.3 disk image: VTOC at track 17 sector 0, catalog chain starting
    /// at track 17 sector 15 (the standard layout), file data allocated from track 18 upward.
    /// </summary>
    private sealed class DskBuilder
    {
        private readonly byte[] _data = new byte[DskParser.DiskImageSize];
        private int _nextDataTrack = 18;
        private int _nextDataSector;
        private int _catalogSector = 15;
        private int _catalogEntryIndex;

        public DskBuilder(byte volume = 254)
        {
            var vtoc = DskParser.SectorOffset(DskParser.VtocTrack, DskParser.VtocSector);
            _data[vtoc + 0x01] = DskParser.VtocTrack;   // first catalog track
            _data[vtoc + 0x02] = 15;                    // first catalog sector
            _data[vtoc + 0x06] = volume;
        }

        public byte[] Build() => _data;

        public DskBuilder AddFile(string name, DskFileType type, byte[] content, bool locked = false, bool deleted = false)
        {
            var (listTrack, listSector) = AllocateSector();
            var listOffset = DskParser.SectorOffset(listTrack, listSector);

            var dataSectorCount = Math.Max(1, (content.Length + DskParser.BytesPerSector - 1) / DskParser.BytesPerSector);
            for (var i = 0; i < dataSectorCount; i++)
            {
                var (dataTrack, dataSector) = AllocateSector();
                _data[listOffset + 0x0C + (i * 2)] = (byte)dataTrack;
                _data[listOffset + 0x0C + (i * 2) + 1] = (byte)dataSector;

                var chunk = content.Skip(i * DskParser.BytesPerSector).Take(DskParser.BytesPerSector).ToArray();
                Array.Copy(chunk, 0, _data, DskParser.SectorOffset(dataTrack, dataSector), chunk.Length);
            }

            WriteCatalogEntry(name, type, locked, deleted, listTrack, listSector, dataSectorCount + 1);
            return this;
        }

        private void WriteCatalogEntry(string name, DskFileType type, bool locked, bool deleted, int listTrack, int listSector, int sectors)
        {
            if (_catalogEntryIndex == DskParser.CatalogEntriesPerSector)
            {
                // Chain a new catalog sector (descending sector numbers, like real DOS).
                var previousOffset = DskParser.SectorOffset(DskParser.VtocTrack, _catalogSector);
                _catalogSector--;
                _data[previousOffset + 0x01] = DskParser.VtocTrack;
                _data[previousOffset + 0x02] = (byte)_catalogSector;
                _catalogEntryIndex = 0;
            }

            var entryOffset = DskParser.SectorOffset(DskParser.VtocTrack, _catalogSector)
                + DskParser.CatalogFirstEntryOffset + (_catalogEntryIndex * DskParser.CatalogEntrySize);
            _catalogEntryIndex++;

            _data[entryOffset] = deleted ? (byte)0xFF : (byte)listTrack;
            _data[entryOffset + 1] = (byte)listSector;
            _data[entryOffset + 2] = (byte)((int)type | (locked ? 0x80 : 0x00));
            for (var i = 0; i < DskParser.FileNameLength; i++)
            {
                var c = i < name.Length ? name[i] : ' ';
                _data[entryOffset + 3 + i] = (byte)(c | 0x80);
            }
            _data[entryOffset + 0x21] = (byte)(sectors & 0xFF);
            _data[entryOffset + 0x22] = (byte)(sectors >> 8);
        }

        private (int Track, int Sector) AllocateSector()
        {
            var result = (_nextDataTrack, _nextDataSector);
            _nextDataSector++;
            if (_nextDataSector == DskParser.SectorsPerTrack)
            {
                _nextDataSector = 0;
                _nextDataTrack++;
            }
            return result;
        }
    }

    private static byte[] BinaryFileBytes(ushort loadAddress, byte[] payload)
        => new byte[]
        {
            (byte)(loadAddress & 0xFF), (byte)(loadAddress >> 8),
            (byte)(payload.Length & 0xFF), (byte)(payload.Length >> 8),
        }.Concat(payload).ToArray();

    private static byte[] ApplesoftFileBytes(byte[] tokenized)
        => new byte[] { (byte)(tokenized.Length & 0xFF), (byte)(tokenized.Length >> 8) }
            .Concat(tokenized).ToArray();

    [Fact]
    public void The_Catalog_Lists_Names_Types_And_Lock_State()
    {
        var disk = DskParser.ParseDskFile(new DskBuilder()
            .AddFile("GAME", DskFileType.Binary, BinaryFileBytes(0x2000, new byte[] { 1, 2, 3 }), locked: true)
            .AddFile("HELLO", DskFileType.ApplesoftBasic, ApplesoftFileBytes(new byte[] { 9, 9 }))
            .Build());

        Assert.Equal(254, disk.Volume);
        Assert.Equal(2, disk.Files.Count);
        Assert.Equal("GAME", disk.Files[0].FileName);
        Assert.Equal(DskFileType.Binary, disk.Files[0].FileType);
        Assert.True(disk.Files[0].Locked);
        Assert.Equal("HELLO", disk.Files[1].FileName);
        Assert.Equal(DskFileType.ApplesoftBasic, disk.Files[1].FileType);
        Assert.False(disk.Files[1].Locked);
    }

    [Fact]
    public void Deleted_Files_Are_Excluded_From_The_Catalog()
    {
        var disk = DskParser.ParseDskFile(new DskBuilder()
            .AddFile("KEEP", DskFileType.Binary, BinaryFileBytes(0x2000, new byte[] { 1 }))
            .AddFile("GONE", DskFileType.Binary, BinaryFileBytes(0x2000, new byte[] { 2 }), deleted: true)
            .Build());

        Assert.Single(disk.Files);
        Assert.Equal("KEEP", disk.Files[0].FileName);
    }

    [Fact]
    public void A_Multi_Sector_File_Concatenates_Its_Sectors_In_Order()
    {
        var payload = Enumerable.Range(0, 600).Select(i => (byte)(i % 251)).ToArray();
        var disk = DskParser.ParseDskFile(new DskBuilder()
            .AddFile("BIG", DskFileType.Binary, BinaryFileBytes(0x4000, payload))
            .Build());

        var fileBytes = disk.ReadBinaryFile("BIG");

        Assert.Equal(0x00, fileBytes[0]);
        Assert.Equal(0x40, fileBytes[1]);
        Assert.Equal(payload, fileBytes[4..]);
    }

    [Fact]
    public void ReadBinaryFile_Trims_Trailing_Sector_Padding()
    {
        var payload = new byte[] { 0xAA, 0xBB, 0xCC };
        var disk = DskParser.ParseDskFile(new DskBuilder()
            .AddFile("SMALL", DskFileType.Binary, BinaryFileBytes(0x0300, payload))
            .Build());

        var fileBytes = disk.ReadBinaryFile("SMALL");

        Assert.Equal(4 + payload.Length, fileBytes.Length);
        Assert.Equal(payload, fileBytes[4..]);
    }

    [Fact]
    public void ReadApplesoftFile_Strips_The_Length_Header_And_Padding()
    {
        var tokenized = new byte[] { 0x10, 0x08, 0x0A, 0x00, 0xBA, 0x00, 0x00, 0x00 };
        var disk = DskParser.ParseDskFile(new DskBuilder()
            .AddFile("PROG", DskFileType.ApplesoftBasic, ApplesoftFileBytes(tokenized))
            .Build());

        Assert.Equal(tokenized, disk.ReadApplesoftFile("PROG"));
    }

    [Fact]
    public void GetFirstRunnableFileName_Prefers_Binary_Over_Applesoft()
    {
        var disk = DskParser.ParseDskFile(new DskBuilder()
            .AddFile("NOTES", DskFileType.Text, new byte[] { 1 })
            .AddFile("LOADER", DskFileType.ApplesoftBasic, ApplesoftFileBytes(new byte[] { 1 }))
            .AddFile("GAME", DskFileType.Binary, BinaryFileBytes(0x2000, new byte[] { 1 }))
            .Build());

        Assert.Equal("GAME", disk.GetFirstRunnableFileName());
    }

    [Fact]
    public void GetFirstRunnableFileName_Falls_Back_To_Applesoft()
    {
        var disk = DskParser.ParseDskFile(new DskBuilder()
            .AddFile("HELLO", DskFileType.ApplesoftBasic, ApplesoftFileBytes(new byte[] { 1 }))
            .Build());

        Assert.Equal("HELLO", disk.GetFirstRunnableFileName());
    }

    [Fact]
    public void A_Catalog_Spanning_Multiple_Sectors_Is_Followed()
    {
        var builder = new DskBuilder();
        for (var i = 0; i < 9; i++)
            builder.AddFile($"FILE{i}", DskFileType.Binary, BinaryFileBytes(0x2000, new byte[] { (byte)i }));

        var disk = DskParser.ParseDskFile(builder.Build());

        Assert.Equal(9, disk.Files.Count);
        Assert.Equal((byte)8, disk.ReadBinaryFile("FILE8")[4]);
    }

    [Fact]
    public void A_Wrongly_Sized_Image_Is_Rejected()
    {
        Assert.Throws<InvalidDataException>(() => DskParser.ParseDskFile(new byte[1000]));
    }

    [Fact]
    public void Reading_A_Missing_File_Throws()
    {
        var disk = DskParser.ParseDskFile(new DskBuilder().Build());

        Assert.Throws<FileNotFoundException>(() => disk.ReadFileContent("NOPE"));
    }
}
