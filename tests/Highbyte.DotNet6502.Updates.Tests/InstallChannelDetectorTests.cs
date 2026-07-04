using Highbyte.DotNet6502.Updates;
using Xunit;

namespace Highbyte.DotNet6502.Updates.Tests;

public class InstallChannelDetectorTests
{
    private static readonly AppUpdateDescriptor TerminalDescriptor = new()
    {
        HomebrewPackage = "dotnet-6502-terminal",
        HomebrewIsCask = false,
        ScoopPackage = "dotnet-6502-terminal",
    };

    private static readonly AppUpdateDescriptor CaskDescriptor = new()
    {
        HomebrewPackage = "dotnet-6502",
        HomebrewIsCask = true,
        ScoopPackage = "dotnet-6502",
    };

    [Fact]
    public void NoMarker_IsNotManaged()
    {
        var probe = new FakeInstallChannelProbe();
        var detector = new InstallChannelDetector(probe);

        var info = detector.Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.NotManaged, info.Channel);
        Assert.False(info.IsManaged);
    }

    [Fact]
    public void HomebrewMarker_ManagerResolvesAndReportsInstalled_IsManaged()
    {
        var probe = new FakeInstallChannelProbe { OS = OSPlatformKind.Linux };
        probe.Files["/app/install-channel"] = "homebrew";
        probe.ResolvableExecutables["brew"] = "/home/linuxbrew/.linuxbrew/bin/brew";
        probe.CommandResults["/home/linuxbrew/.linuxbrew/bin/brew list --versions dotnet-6502-terminal"] =
            new ProcessRunResult(0, "dotnet-6502-terminal 0.40.2\n");

        var info = new InstallChannelDetector(probe).Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.Homebrew, info.Channel);
        Assert.Equal("dotnet-6502-terminal", info.PackageName);
        Assert.Equal("/home/linuxbrew/.linuxbrew/bin/brew", info.ManagerExecutablePath);
    }

    [Fact]
    public void HomebrewMarker_ManagerNotOnPath_IsNotManaged()
    {
        var probe = new FakeInstallChannelProbe { OS = OSPlatformKind.Linux };
        probe.Files["/app/install-channel"] = "homebrew";
        // No brew resolvable.

        var info = new InstallChannelDetector(probe).Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.NotManaged, info.Channel);
    }

    [Fact]
    public void HomebrewMarker_ManagerReportsNotInstalled_IsNotManaged()
    {
        // Stale marker on a portable copy: manager exists but the package isn't installed → safe fallback.
        var probe = new FakeInstallChannelProbe { OS = OSPlatformKind.Linux };
        probe.Files["/app/install-channel"] = "homebrew";
        probe.ResolvableExecutables["brew"] = "/opt/homebrew/bin/brew";
        probe.CommandResults["/opt/homebrew/bin/brew list --versions dotnet-6502-terminal"] =
            new ProcessRunResult(1, "Error: No such keg\n");

        var info = new InstallChannelDetector(probe).Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.NotManaged, info.Channel);
    }

    [Fact]
    public void CaskMarker_InSupportDir_ConfirmedViaCaskQuery_IsManaged()
    {
        var probe = new FakeInstallChannelProbe { OS = OSPlatformKind.MacOS, HomeDirectory = "/Users/me" };
        var markerPath = "/Users/me/Library/Application Support/Highbyte/DotNet6502/install-channel";
        probe.Files[markerPath] = "homebrew:dotnet-6502";
        probe.ResolvableExecutables["brew"] = "/opt/homebrew/bin/brew";
        probe.CommandResults["/opt/homebrew/bin/brew list --cask --versions dotnet-6502"] =
            new ProcessRunResult(0, "dotnet-6502 0.40.2\n");

        var info = new InstallChannelDetector(probe).Detect(CaskDescriptor);

        Assert.Equal(InstallChannel.Homebrew, info.Channel);
        Assert.Equal("dotnet-6502", info.PackageName);
    }

    [Fact]
    public void ScoopMarker_ManagerResolvesAndReportsInstalled_IsManaged()
    {
        var probe = new FakeInstallChannelProbe { OS = OSPlatformKind.Windows, HomeDirectory = @"C:\Users\me" };
        probe.Files[@"/app/install-channel".Replace('/', Path.DirectorySeparatorChar)] = "scoop";
        probe.BaseDirectory = "/app".Replace('/', Path.DirectorySeparatorChar);
        probe.ResolvableExecutables["scoop"] = @"C:\Users\me\scoop\shims\scoop";
        probe.CommandResults[@"C:\Users\me\scoop\shims\scoop list dotnet-6502-terminal"] =
            new ProcessRunResult(0, "Installed apps:\n\ndotnet-6502-terminal 0.40.2\n");

        var info = new InstallChannelDetector(probe).Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.Scoop, info.Channel);
        Assert.Equal("dotnet-6502-terminal", info.PackageName);
    }

    [Fact]
    public void CaskSupportDirMarkerPath_NullOnNonMac()
    {
        var probe = new FakeInstallChannelProbe { OS = OSPlatformKind.Windows };
        Assert.Null(new InstallChannelDetector(probe).CaskSupportDirMarkerPath());
    }
}
