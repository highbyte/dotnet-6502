using Highbyte.DotNet6502.App.RemoteClient;
using Xunit;

namespace Highbyte.DotNet6502.App.RemoteClient.Tests;

/// <summary>
/// The client validates commands against its own switch before sending, so a command missing
/// from the builder is rejected locally as "Unknown command" even when the server supports it.
/// </summary>
public class Apple2DiskCommandBuildTests
{
    [Theory]
    [InlineData("apple2.bootdisk")]
    [InlineData("apple2.ejectdisk")]
    [InlineData("apple2.diskstatus")]
    public void Parameterless_disk_commands_build_without_error(string cmd)
    {
        var result = RemoteClientRequestBuilder.Build([cmd]);

        Assert.Null(result.Error);
        Assert.NotNull(result.Request);
        Assert.Equal(cmd, result.Request!["cmd"]);
    }

    [Fact]
    public void InsertDisk_encodes_the_file_as_base64()
    {
        var diskBytes = new byte[] { 1, 2, 3, 4 };
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, diskBytes);

            var result = RemoteClientRequestBuilder.Build(["apple2.insertdisk", "--file", path]);

            Assert.Null(result.Error);
            Assert.Equal(Convert.ToBase64String(diskBytes), result.Request!["data"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InsertDisk_passes_through_explicit_base64_data()
    {
        var result = RemoteClientRequestBuilder.Build(["apple2.insertdisk", "--data", "AQID"]);

        Assert.Null(result.Error);
        Assert.Equal("AQID", result.Request!["data"]);
    }

    [Fact]
    public void InsertDisk_without_file_or_data_is_rejected()
    {
        var result = RemoteClientRequestBuilder.Build(["apple2.insertdisk"]);

        Assert.NotNull(result.Error);
        Assert.Contains("--file", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void InsertDisk_with_a_missing_file_is_rejected()
    {
        var result = RemoteClientRequestBuilder.Build(["apple2.insertdisk", "--file", "/no/such/disk-image.dsk"]);

        Assert.NotNull(result.Error);
        Assert.Contains("File not found", result.Error!, StringComparison.Ordinal);
    }
}
