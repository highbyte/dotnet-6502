using Highbyte.DotNet6502.Updates;
using Xunit;

namespace Highbyte.DotNet6502.Updates.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("0.40.2-alpha", "0.40.2-alpha")]
    [InlineData("0.40.2-alpha+abc123", "0.40.2-alpha")] // SourceLink build metadata stripped
    [InlineData("1.2.3", "1.2.3")]
    public void Parse_ReturnsStampedVersion(string informational, string expected)
    {
        var v = AppVersion.Parse(informational);
        Assert.NotNull(v);
        Assert.Equal(expected, v!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0.0")]          // unstamped default
    [InlineData("1.0.0+abc123")]   // unstamped + SourceLink hash
    [InlineData("not-a-version")]
    public void Parse_ReturnsNullForUnknownOrUnstamped(string? informational)
    {
        Assert.Null(AppVersion.Parse(informational));
    }
}
