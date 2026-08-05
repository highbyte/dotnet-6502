using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Systems.Tests.Download;

public class RomDownloadSourceTests
{
    [Fact]
    public void A_Bare_File_Source_Is_Not_A_Zip_Archive()
    {
        var source = new RomDownloadSource("https://example.com/roms/apple.rom");

        Assert.False(source.IsZipArchive);
        Assert.Equal("apple.rom", source.ResolveFileName());
        Assert.Equal("https://example.com/roms/apple.rom", source.ResolveCacheKey());
        Assert.Equal("rom", source.ResolveExtension());
    }

    [Fact]
    public void A_Zip_Source_Saves_Under_The_Entry_Name_Not_The_Archive_Name()
    {
        var source = new RomDownloadSource("https://example.com/roms/ROMS.ZIP", ZipEntryName: "3410036.BIN");

        Assert.True(source.IsZipArchive);
        Assert.Equal("3410036.BIN", source.ResolveFileName());
        Assert.Equal("bin", source.ResolveExtension());
    }

    [Fact]
    public void An_Explicit_File_Name_Wins()
    {
        var source = new RomDownloadSource(
            "https://example.com/roms/ROMS.ZIP",
            ZipEntryName: "3410036.BIN",
            FileName: "apple2-chargen.bin");

        Assert.Equal("apple2-chargen.bin", source.ResolveFileName());
    }

    [Fact]
    public void A_Nested_Zip_Entry_Resolves_To_Its_Leaf_Name()
    {
        var source = new RomDownloadSource("https://example.com/a.zip", ZipEntryName: "roms/apple/3410036.BIN");

        Assert.Equal("3410036.BIN", source.ResolveFileName());
    }

    [Fact]
    public void The_Cache_Key_Includes_The_Zip_Entry_So_Entries_Of_One_Archive_Do_Not_Collide()
    {
        var first = new RomDownloadSource("https://example.com/ROMS.ZIP", ZipEntryName: "3410036.BIN");
        var second = new RomDownloadSource("https://example.com/ROMS.ZIP", ZipEntryName: "341011d0.bin");

        Assert.NotEqual(first.ResolveCacheKey(), second.ResolveCacheKey());
        Assert.StartsWith("https://example.com/ROMS.ZIP#", first.ResolveCacheKey());
    }
}
