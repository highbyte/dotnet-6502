using System.IO.Compression;
using Highbyte.DotNet6502.Systems.Commodore64.Utils;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class ZipImageExtractorTests
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
    public void EnsureImageBytes_Returns_Raw_Bytes_Unchanged()
    {
        var raw = new byte[] { 0x10, 0x20, 0x30 };

        var result = ZipImageExtractor.EnsureImageBytes(
            raw,
            ".crt",
            ZipImageMultipleMatchBehavior.Throw);

        Assert.Same(raw, result);
    }

    [Fact]
    public void EnsureImageBytes_Extracts_Matching_Extension_From_Zip()
    {
        var crtContent = new byte[] { 0x43, 0x36, 0x34 };
        var zip = BuildZipWith(("readme.txt", new byte[] { 1 }), ("fc3.crt", crtContent));

        var result = ZipImageExtractor.EnsureImageBytes(
            zip,
            ".crt",
            ZipImageMultipleMatchBehavior.Throw);

        Assert.Equal(crtContent, result);
    }

    [Fact]
    public void EnsureImageBytes_Normalizes_Extension()
    {
        var crtContent = new byte[] { 0x01 };
        var zip = BuildZipWith(("fc3.crt", crtContent));

        var result = ZipImageExtractor.EnsureImageBytes(
            zip,
            "crt",
            ZipImageMultipleMatchBehavior.Throw);

        Assert.Equal(crtContent, result);
    }

    [Fact]
    public void EnsureImageBytes_Throws_When_Multiple_Matches_Are_Ambiguous()
    {
        var zip = BuildZipWith(
            ("fc3.crt", new byte[] { 0x01 }),
            ("expert.crt", new byte[] { 0x02 }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => ZipImageExtractor.EnsureImageBytes(
                zip,
                ".crt",
                ZipImageMultipleMatchBehavior.Throw));

        Assert.Contains("Multiple .crt files", exception.Message);
    }

    [Fact]
    public void EnsureImageBytes_Uses_First_Match_When_Configured()
    {
        var first = new byte[] { 0x01 };
        var second = new byte[] { 0x02 };
        var zip = BuildZipWith(("fc3.crt", first), ("expert.crt", second));

        var result = ZipImageExtractor.EnsureImageBytes(
            zip,
            ".crt",
            ZipImageMultipleMatchBehavior.UseFirst);

        Assert.Equal(first, result);
    }

    [Fact]
    public void EnsureImageBytes_Extracts_Explicit_Zip_Entry()
    {
        var first = new byte[] { 0x01 };
        var second = new byte[] { 0x02 };
        var zip = BuildZipWith(("fc3.crt", first), ("side-b/expert.crt", second));

        var result = ZipImageExtractor.EnsureImageBytes(
            zip,
            ".crt",
            ZipImageMultipleMatchBehavior.Throw,
            entryName: "side-b/expert.crt");

        Assert.Equal(second, result);
    }

    [Fact]
    public void EnsureImageBytes_Normalizes_Explicit_Zip_Entry_Slashes()
    {
        var crtContent = new byte[] { 0x01 };
        var zip = BuildZipWith(("carts/fc3.crt", crtContent));

        var result = ZipImageExtractor.EnsureImageBytes(
            zip,
            ".crt",
            ZipImageMultipleMatchBehavior.Throw,
            entryName: @"\carts\fc3.crt");

        Assert.Equal(crtContent, result);
    }

    [Fact]
    public void EnsureImageBytes_Throws_When_Explicit_Zip_Entry_Has_Wrong_Extension()
    {
        var zip = BuildZipWith(("readme.txt", new byte[] { 0x01 }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => ZipImageExtractor.EnsureImageBytes(
                zip,
                ".crt",
                ZipImageMultipleMatchBehavior.Throw,
                entryName: "readme.txt"));

        Assert.Contains("must be a .crt file", exception.Message);
    }

    [Fact]
    public void EnsureImageBytes_Throws_When_Explicit_Zip_Entry_Is_Missing()
    {
        var zip = BuildZipWith(("fc3.crt", new byte[] { 0x01 }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => ZipImageExtractor.EnsureImageBytes(
                zip,
                ".crt",
                ZipImageMultipleMatchBehavior.Throw,
                entryName: "expert.crt"));

        Assert.Contains("was not found", exception.Message);
    }

    [Fact]
    public void EnsureImageBytes_Throws_When_Explicit_Zip_Entry_Is_Supplied_For_Raw_Bytes()
    {
        var raw = new byte[] { 0x10, 0x20, 0x30 };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ZipImageExtractor.EnsureImageBytes(
                raw,
                ".crt",
                ZipImageMultipleMatchBehavior.Throw,
                entryName: "fc3.crt"));

        Assert.Contains("not a ZIP archive", exception.Message);
    }

    [Fact]
    public void ExtractImageFromZip_Throws_When_No_Matching_Entry()
    {
        var zip = BuildZipWith(("readme.txt", new byte[] { 1 }));

        Assert.Throws<InvalidOperationException>(
            () => ZipImageExtractor.ExtractImageFromZip(
                zip,
                ".crt",
                ZipImageMultipleMatchBehavior.Throw));
    }
}
