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

    // --- Path fallback (no marker): still confirmed by the package manager, still biased to not-managed. ---

    [Fact]
    public void NoMarker_ScoopAppsCurrentPath_ConfirmedByManager_IsManaged()
    {
        var probe = new FakeInstallChannelProbe
        {
            OS = OSPlatformKind.Windows,
            HomeDirectory = @"C:\Users\me",
            BaseDirectory = @"C:\Users\me\scoop\apps\dotnet-6502-terminal\current",
        };
        // No marker file — the Scoop apps/<pkg>/current layout is the only signal.
        probe.ResolvableExecutables["scoop"] = @"C:\Users\me\scoop\shims\scoop";
        probe.CommandResults[@"C:\Users\me\scoop\shims\scoop list dotnet-6502-terminal"] =
            new ProcessRunResult(0, "Installed apps:\n\ndotnet-6502-terminal 0.40.2\n");

        var info = new InstallChannelDetector(probe).Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.Scoop, info.Channel);
        Assert.Equal("dotnet-6502-terminal", info.PackageName);
    }

    [Fact]
    public void NoMarker_HomebrewCellarPath_ConfirmedByManager_IsManaged()
    {
        var probe = new FakeInstallChannelProbe
        {
            OS = OSPlatformKind.MacOS,
            BaseDirectory = "/opt/homebrew/Cellar/dotnet-6502-terminal/0.40.2/libexec",
        };
        // No marker file — the Homebrew Cellar/<formula>/<version> layout is the only signal.
        probe.ResolvableExecutables["brew"] = "/opt/homebrew/bin/brew";
        probe.CommandResults["/opt/homebrew/bin/brew list --versions dotnet-6502-terminal"] =
            new ProcessRunResult(0, "dotnet-6502-terminal 0.40.2\n");

        var info = new InstallChannelDetector(probe).Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.Homebrew, info.Channel);
        Assert.Equal("dotnet-6502-terminal", info.PackageName);
    }

    [Fact]
    public void NoMarker_ScoopPath_ButManagerReportsNotInstalled_IsNotManaged()
    {
        // A portable copy that merely happens to live under a scoop-shaped path: the manager query
        // (the authoritative confirmation) must still veto it.
        var probe = new FakeInstallChannelProbe
        {
            OS = OSPlatformKind.Windows,
            HomeDirectory = @"C:\Users\me",
            BaseDirectory = @"C:\Users\me\scoop\apps\dotnet-6502-terminal\current",
        };
        probe.ResolvableExecutables["scoop"] = @"C:\Users\me\scoop\shims\scoop";
        probe.CommandResults[@"C:\Users\me\scoop\shims\scoop list dotnet-6502-terminal"] =
            new ProcessRunResult(1, "");

        var info = new InstallChannelDetector(probe).Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.NotManaged, info.Channel);
    }

    [Fact]
    public void NoMarker_HomebrewCellarPath_ButManagerNotResolvable_IsNotManaged()
    {
        var probe = new FakeInstallChannelProbe
        {
            OS = OSPlatformKind.MacOS,
            BaseDirectory = "/opt/homebrew/Cellar/dotnet-6502-terminal/0.40.2/libexec",
        };
        // No brew resolvable ⇒ can't confirm ⇒ not-managed.

        var info = new InstallChannelDetector(probe).Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.NotManaged, info.Channel);
    }

    [Fact]
    public void NoMarker_CaskAppInApplications_IsNotManaged()
    {
        // The cask stays marker-driven: the .app runs from /Applications, not a Cellar, so there is no
        // path fallback for it even if brew is installed and would report it.
        var probe = new FakeInstallChannelProbe
        {
            OS = OSPlatformKind.MacOS,
            HomeDirectory = "/Users/me",
            BaseDirectory = "/Applications/DotNet 6502 Emulator.app/Contents/MacOS",
        };
        probe.ResolvableExecutables["brew"] = "/opt/homebrew/bin/brew";
        probe.CommandResults["/opt/homebrew/bin/brew list --cask --versions dotnet-6502"] =
            new ProcessRunResult(0, "dotnet-6502 0.40.2\n");

        var info = new InstallChannelDetector(probe).Detect(CaskDescriptor);

        Assert.Equal(InstallChannel.NotManaged, info.Channel);
    }

    [Fact]
    public void NoMarker_HomebrewCellarPath_ForADifferentFormula_IsNotManaged()
    {
        // The Cellar directory is named after the formula; a different formula's path must not match.
        var probe = new FakeInstallChannelProbe
        {
            OS = OSPlatformKind.MacOS,
            BaseDirectory = "/opt/homebrew/Cellar/some-other-tool/1.0.0/libexec",
        };
        probe.ResolvableExecutables["brew"] = "/opt/homebrew/bin/brew";

        var info = new InstallChannelDetector(probe).Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.NotManaged, info.Channel);
    }

    [Fact]
    public void NoMarker_CustomScoopRootPath_ConfirmedByManager_IsManaged()
    {
        // A $SCOOP root that isn't literally named "scoop" is still recognised via the apps/<pkg>/current tail.
        var probe = new FakeInstallChannelProbe
        {
            OS = OSPlatformKind.Windows,
            HomeDirectory = @"C:\Users\me",
            BaseDirectory = @"D:\tools\scooproot\apps\dotnet-6502-terminal\current",
        };
        probe.EnvironmentVariables["SCOOP"] = @"D:\tools\scooproot";
        probe.ResolvableExecutables["scoop"] = @"D:\tools\scooproot\shims\scoop";
        probe.CommandResults[@"D:\tools\scooproot\shims\scoop list dotnet-6502-terminal"] =
            new ProcessRunResult(0, "dotnet-6502-terminal 0.40.2\n");

        var info = new InstallChannelDetector(probe).Detect(TerminalDescriptor);

        Assert.Equal(InstallChannel.Scoop, info.Channel);
    }
}
