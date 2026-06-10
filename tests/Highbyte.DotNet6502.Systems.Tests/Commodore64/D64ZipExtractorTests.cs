using System.IO.Compression;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class D64ZipExtractorTests
{
    private static byte[] BuildZipWith(params (string name, byte[] content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }
        return ms.ToArray();
    }

    [Fact]
    public void LooksLikeZip_Detects_Zip_Magic()
    {
        var zip = BuildZipWith(("game.d64", new byte[] { 1, 2, 3 }));
        Assert.True(D64ZipExtractor.LooksLikeZip(zip));
    }

    [Fact]
    public void LooksLikeZip_Returns_False_For_Raw_Bytes()
    {
        var raw = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        Assert.False(D64ZipExtractor.LooksLikeZip(raw));
    }

    [Fact]
    public void LooksLikeZip_Returns_False_For_Short_Buffer()
    {
        Assert.False(D64ZipExtractor.LooksLikeZip(new byte[] { 0x50, 0x4B }));
    }

    [Fact]
    public void EnsureD64Bytes_Returns_Raw_Bytes_Unchanged()
    {
        var raw = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
        var result = D64ZipExtractor.EnsureD64Bytes(raw);
        Assert.Same(raw, result);
    }

    [Fact]
    public void EnsureD64Bytes_Extracts_D64_From_Zip()
    {
        var d64Content = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var zip = BuildZipWith(("readme.txt", new byte[] { 1 }), ("disk.d64", d64Content));

        var result = D64ZipExtractor.EnsureD64Bytes(zip);

        Assert.Equal(d64Content, result);
    }

    [Fact]
    public void ExtractFirstD64FromZip_Throws_When_No_D64_Entry()
    {
        var zip = BuildZipWith(("readme.txt", new byte[] { 1 }), ("game.prg", new byte[] { 2 }));
        Assert.Throws<InvalidOperationException>(() => D64ZipExtractor.ExtractFirstD64FromZip(zip));
    }
}
