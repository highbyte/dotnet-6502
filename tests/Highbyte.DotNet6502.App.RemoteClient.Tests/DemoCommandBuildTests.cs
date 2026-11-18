using Highbyte.DotNet6502.App.RemoteClient;
using Xunit;

namespace Highbyte.DotNet6502.App.RemoteClient.Tests;

public class DemoCommandBuildTests
{
    [Theory]
    [InlineData("demo.record")]
    [InlineData("demo.open")]
    public void File_commands_require_server_path(string command)
    {
        Assert.Contains("--path", RemoteClientRequestBuilder.Build([command]).Error!);
        var result = RemoteClientRequestBuilder.Build([command, "--path", "/server/demo.d6502demo"]);
        Assert.Null(result.Error);
        Assert.Equal("/server/demo.d6502demo", result.Request!["path"]);
    }

    [Theory]
    [InlineData("demo.state")]
    [InlineData("demo.stop")]
    [InlineData("demo.restart")]
    [InlineData("demo.save")]
    public void Session_commands_build_without_a_path(string command)
    {
        var result = RemoteClientRequestBuilder.Build([command]);
        Assert.Null(result.Error);
        Assert.Equal(command, result.Request!["cmd"]);
    }
}
