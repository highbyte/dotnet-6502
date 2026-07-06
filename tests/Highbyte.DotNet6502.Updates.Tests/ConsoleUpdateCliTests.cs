using Highbyte.DotNet6502.Updates;
using Xunit;

namespace Highbyte.DotNet6502.Updates.Tests;

public class ConsoleUpdateCliTests
{
    [Theory]
    [InlineData(new[] { "--version" }, true)]
    [InlineData(new[] { "--check-update" }, true)]
    [InlineData(new[] { "--update" }, true)]
    [InlineData(new[] { "--start", "--check-update", "--system", "C64" }, true)]
    [InlineData(new[] { "--start", "--system", "C64" }, false)]
    [InlineData(new string[0], false)]
    public void WantsHandling_DetectsFlags(string[] args, bool expected)
        => Assert.Equal(expected, ConsoleUpdateCli.WantsHandling(args));

    [Fact]
    public void FormatNoticeLine_MatchesSpecWording()
    {
        SemanticVersion.TryParse("0.40.2-alpha", out var latest);
        SemanticVersion.TryParse("0.39.3-alpha", out var current);
        var result = new UpdateCheckResult
        {
            Status = UpdateCheckStatus.UpdateAvailable,
            CurrentVersion = current,
            LatestVersion = latest,
            SuggestedCommand = "brew update && brew upgrade dotnet-6502-terminal",
        };

        Assert.Equal(
            "A newer version v0.40.2-alpha is available (you have v0.39.3-alpha). Run 'brew update && brew upgrade dotnet-6502-terminal' to update.",
            ConsoleUpdateCli.FormatNoticeLine(result));
    }

    [Fact]
    public void IsSuppressedByEnvironment_WhenOptOutEnvSet()
    {
        var previous = Environment.GetEnvironmentVariable(ConsoleUpdateCli.OptOutEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(ConsoleUpdateCli.OptOutEnvVar, "1");
            Assert.True(ConsoleUpdateCli.IsSuppressedByEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConsoleUpdateCli.OptOutEnvVar, previous);
        }
    }
}
