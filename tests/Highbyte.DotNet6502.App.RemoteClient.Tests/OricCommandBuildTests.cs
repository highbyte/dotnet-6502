using Highbyte.DotNet6502.App.RemoteClient;
using Xunit;

namespace Highbyte.DotNet6502.App.RemoteClient.Tests;

public class OricCommandBuildTests
{
    [Theory]
    [InlineData("oric.isbasicstarted")]
    [InlineData("oric.getbasicsource")]
    [InlineData("oric.rewindtape")]
    [InlineData("oric.ejecttape")]
    [InlineData("oric.tapestatus")]
    public void Parameterless_commands_build_without_error(string cmd)
    {
        var result = RemoteClientRequestBuilder.Build([cmd]);

        Assert.Null(result.Error);
        Assert.Equal(cmd, result.Request!["cmd"]);
    }

    [Fact]
    public void Type_passes_through_text()
    {
        var result = RemoteClientRequestBuilder.Build(["oric.type", "--text", "CLOAD\"\"\n"]);

        Assert.Null(result.Error);
        Assert.Equal("CLOAD\"\"\n", result.Request!["text"]);
    }

    [Theory]
    [InlineData("oric.loadtap")]
    [InlineData("oric.inserttape")]
    public void Tap_commands_encode_a_file_as_base64(string cmd)
    {
        var tapBytes = new byte[] { 0x16, 0x16, 0x24, 0x00 };
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, tapBytes);

            var result = RemoteClientRequestBuilder.Build([cmd, "--file", path]);

            Assert.Null(result.Error);
            Assert.Equal(Convert.ToBase64String(tapBytes), result.Request!["data"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("oric.loadtap")]
    [InlineData("oric.inserttape")]
    public void Tap_commands_accept_explicit_base64_data(string cmd)
    {
        var result = RemoteClientRequestBuilder.Build([cmd, "--data", "FhYkAA=="]);

        Assert.Null(result.Error);
        Assert.Equal("FhYkAA==", result.Request!["data"]);
    }

    [Theory]
    [InlineData("oric.loadtap")]
    [InlineData("oric.inserttape")]
    public void Tap_commands_require_file_or_data(string cmd)
    {
        var result = RemoteClientRequestBuilder.Build([cmd]);

        Assert.Contains("--file", result.Error!, StringComparison.Ordinal);
    }
}
