using Highbyte.DotNet6502.Updates;
using Xunit;

namespace Highbyte.DotNet6502.Updates.Tests;

public class SemanticVersionTests
{
    [Theory]
    [InlineData("0.40.2", 0, 40, 2, false)]
    [InlineData("1.2.3-alpha", 1, 2, 3, true)]
    [InlineData("v0.40.2-alpha", 0, 40, 2, true)]       // leading v tolerated (git tag form)
    [InlineData("0.40.2-alpha+abc123", 0, 40, 2, true)] // build metadata stripped
    [InlineData("10.20.30", 10, 20, 30, false)]
    public void TryParse_ParsesValidVersions(string input, int major, int minor, int patch, bool isPrerelease)
    {
        Assert.True(SemanticVersion.TryParse(input, out var v));
        Assert.NotNull(v);
        Assert.Equal(major, v!.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(patch, v.Patch);
        Assert.Equal(isPrerelease, v.IsPrerelease);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.x")]
    [InlineData("1.2.3-")]        // empty prerelease
    [InlineData("1.2.3-alpha..1")] // empty identifier
    [InlineData("1.2.3+")]        // empty build metadata
    public void TryParse_RejectsInvalidVersions(string? input)
    {
        Assert.False(SemanticVersion.TryParse(input, out var v));
        Assert.Null(v);
    }

    [Theory]
    // Core precedence.
    [InlineData("0.40.1", "0.40.2", -1)]
    [InlineData("0.41.0", "0.40.9", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    // Release outranks prerelease of same core.
    [InlineData("0.40.2-alpha", "0.40.2", -1)]
    [InlineData("0.40.2", "0.40.2-alpha", 1)]
    // Prerelease ordering (from the feature spec's examples).
    [InlineData("0.40.1-alpha", "0.40.2-alpha", -1)]
    [InlineData("0.40.2-alpha", "0.40.2-alpha.1", -1)] // more identifiers = higher
    [InlineData("0.40.2-alpha.1", "0.40.2-alpha.2", -1)]
    [InlineData("0.40.2-alpha.2", "0.40.2-alpha.10", -1)] // numeric identifiers compared numerically
    [InlineData("0.40.2-alpha", "0.40.2-beta", -1)]       // lexical
    [InlineData("0.40.2-1", "0.40.2-alpha", -1)]          // numeric < alphanumeric
    public void CompareTo_OrdersBySemverPrecedence(string a, string b, int expectedSign)
    {
        Assert.True(SemanticVersion.TryParse(a, out var va));
        Assert.True(SemanticVersion.TryParse(b, out var vb));
        Assert.Equal(expectedSign, Math.Sign(va!.CompareTo(vb!)));
        Assert.Equal(-expectedSign, Math.Sign(vb!.CompareTo(va!)));
    }

    [Fact]
    public void BuildMetadata_IgnoredForPrecedence()
    {
        Assert.True(SemanticVersion.TryParse("0.40.2-alpha+build1", out var a));
        Assert.True(SemanticVersion.TryParse("0.40.2-alpha+build2", out var b));
        Assert.Equal(0, a!.CompareTo(b!));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Operators_Work()
    {
        SemanticVersion.TryParse("0.40.2-alpha", out var older);
        SemanticVersion.TryParse("0.40.2-alpha", out var olderCopy);
        SemanticVersion.TryParse("0.41.0-alpha", out var newer);
        Assert.True(newer! > older!);
        Assert.True(older! < newer!);
        Assert.True(older! <= olderCopy!);
        Assert.False(older! > newer!);
    }
}
