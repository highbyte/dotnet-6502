using Highbyte.DotNet6502.Updates;
using Xunit;

namespace Highbyte.DotNet6502.Updates.Tests;

public class AppUpdateDescriptorTests
{
    [Fact]
    public void HomebrewCaskUpgradeCommandArgs_UpdateTapBeforeUpgrade()
    {
        var descriptor = new AppUpdateDescriptor
        {
            HomebrewPackage = "dotnet-6502",
            HomebrewIsCask = true,
            ScoopPackage = "dotnet-6502",
        };

        var commands = descriptor.UpgradeCommandArgs(InstallChannel.Homebrew);

        Assert.Collection(
            commands,
            command => Assert.Equal(new[] { "update" }, command),
            command => Assert.Equal(new[] { "upgrade", "--cask", "dotnet-6502" }, command));
    }

    [Fact]
    public void ScoopUpgradeCommandArgs_RefreshesBucketsBeforeUpgrade()
    {
        var descriptor = new AppUpdateDescriptor
        {
            HomebrewPackage = "dotnet-6502",
            ScoopPackage = "dotnet-6502",
        };

        var commands = descriptor.UpgradeCommandArgs(InstallChannel.Scoop);

        // Bare `scoop update` git-pulls the bucket manifests first; without it `scoop update <app>`
        // upgrades against a stale manifest and never sees a freshly published version.
        Assert.Collection(
            commands,
            command => Assert.Equal(new[] { "update" }, command),
            command => Assert.Equal(new[] { "update", "dotnet-6502" }, command));
    }
}
