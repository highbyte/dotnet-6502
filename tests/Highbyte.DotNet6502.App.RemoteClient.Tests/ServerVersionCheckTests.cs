using Highbyte.DotNet6502.App.RemoteClient;
using Highbyte.DotNet6502.Updates;
using Xunit;

namespace Highbyte.DotNet6502.App.RemoteClient.Tests;

public class ServerVersionCheckTests
{
    private static SemanticVersion V(string s)
    {
        Assert.True(SemanticVersion.TryParse(s, out var v) && v is not null, $"bad test version '{s}'");
        return v!;
    }

    [Fact]
    public void MatchingVersions_succeeds_and_reports_ok_on_stdout()
    {
        var result = ServerVersionCheck.Evaluate("Headless", V("0.41.0-alpha"), V("0.41.0-alpha"));

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StderrLines);
        Assert.Contains(result.StdoutLines, l => l.Contains("OK") && l.Contains("matches"));
    }

    [Fact]
    public void ServerNewer_warns_on_stderr_with_mismatch_exit_code_and_direction()
    {
        var result = ServerVersionCheck.Evaluate("Avalonia", V("0.42.0-alpha"), V("0.41.0-alpha"));

        Assert.Equal(ServerVersionCheck.MismatchExitCode, result.ExitCode);
        Assert.Empty(result.StdoutLines);
        Assert.Contains(result.StderrLines, l => l.Contains("newer than"));
        // Server package hint should be for the Avalonia app package, not the client package.
        Assert.Contains(result.StderrLines, l => l.Contains("dotnet-6502 ") || l.Contains("--cask dotnet-6502"));
    }

    [Fact]
    public void ServerOlder_warns_with_older_direction()
    {
        var result = ServerVersionCheck.Evaluate("Headless", V("0.40.0-alpha"), V("0.41.0-alpha"));

        Assert.Equal(ServerVersionCheck.MismatchExitCode, result.ExitCode);
        Assert.Contains(result.StderrLines, l => l.Contains("older than"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void UnknownVersion_cannot_verify_and_exits_zero(bool serverUnknown, bool clientUnknown)
    {
        var server = serverUnknown ? null : V("0.41.0-alpha");
        var client = clientUnknown ? null : V("0.41.0-alpha");

        var result = ServerVersionCheck.Evaluate("Headless", server, client);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StdoutLines);
        Assert.Contains(result.StderrLines, l => l.Contains("Cannot verify"));
    }

    [Fact]
    public void ServerTooOld_is_treated_as_mismatch()
    {
        var result = ServerVersionCheck.ServerTooOld("Headless");

        Assert.Equal(ServerVersionCheck.MismatchExitCode, result.ExitCode);
        Assert.Contains(result.StderrLines, l => l.Contains("predates"));
    }
}
