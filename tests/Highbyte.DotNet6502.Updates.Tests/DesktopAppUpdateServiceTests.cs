using Highbyte.DotNet6502.App.Avalonia.Desktop;
using Highbyte.DotNet6502.Updates;
using Xunit;

namespace Highbyte.DotNet6502.Updates.Tests;

public class DesktopAppUpdateServiceTests
{
    [Fact]
    public void BuildRelaunchSpec_LinuxHomebrew_UsesStableBrewBinLauncher()
    {
        var spec = DesktopAppUpdateService.BuildRelaunchSpec(
            InstallChannel.Homebrew,
            "/home/linuxbrew/.linuxbrew/bin/brew",
            "dotnet-6502",
            "/home/linuxbrew/.linuxbrew/Cellar/dotnet-6502/0.41.4-alpha/bin/dotnet-6502",
            new[] { "dotnet-6502", "--console-log" },
            OSPlatformKind.Linux);

        Assert.Equal("/home/linuxbrew/.linuxbrew/bin/dotnet-6502", spec.Executable);
        Assert.Equal(new[] { "--console-log" }, spec.Arguments);
    }

    [Fact]
    public void BuildRelaunchSpec_DotNetHost_ReconstructsOriginalLaunch()
    {
        var spec = DesktopAppUpdateService.BuildRelaunchSpec(
            InstallChannel.Homebrew,
            "/home/linuxbrew/.linuxbrew/bin/brew",
            "dotnet-6502",
            "/usr/bin/dotnet",
            new[] { "/usr/bin/dotnet", "/tmp/app/Highbyte.DotNet6502.App.Avalonia.Desktop.dll", "--console-log" },
            OSPlatformKind.Linux);

        Assert.Equal("/usr/bin/dotnet", spec.Executable);
        Assert.Equal(
            new[] { "/usr/bin/dotnet", "/tmp/app/Highbyte.DotNet6502.App.Avalonia.Desktop.dll", "--console-log" },
            spec.Arguments);
    }

    [Fact]
    public void BuildRelaunchSpec_WindowsScoop_UsesCurrentProcessPath()
    {
        var spec = DesktopAppUpdateService.BuildRelaunchSpec(
            InstallChannel.Scoop,
            @"C:\Users\me\scoop\shims\scoop.ps1",
            "dotnet-6502",
            @"C:\Users\me\scoop\apps\dotnet-6502\current\dotnet-6502.exe",
            new[] { "dotnet-6502", "--console-log" },
            OSPlatformKind.Windows);

        Assert.Equal(@"C:\Users\me\scoop\apps\dotnet-6502\current\dotnet-6502.exe", spec.Executable);
        Assert.Equal(new[] { "--console-log" }, spec.Arguments);
    }
}
