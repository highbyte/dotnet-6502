using Highbyte.DotNet6502.Updates;
using Xunit;

namespace Highbyte.DotNet6502.Updates.Tests;

public class ProcessLaunchTests
{
    [Fact]
    public void Windows_CmdShim_IsRunViaCmdExe()
    {
        // The Scoop shim: C:\Users\me\scoop\shims\scoop.cmd list dotnet-6502
        var psi = ProcessLaunch.BuildStartInfo(@"C:\Users\me\scoop\shims\scoop.cmd", new[] { "list", "dotnet-6502" }, isWindows: true);

        Assert.Equal("cmd.exe", psi.FileName);
        Assert.Equal(new[] { "/c", @"C:\Users\me\scoop\shims\scoop.cmd", "list", "dotnet-6502" }, psi.ArgumentList);
        Assert.False(psi.UseShellExecute);
    }

    [Fact]
    public void Windows_Ps1Shim_IsRunViaPowerShellFile_DefaultsToWindowsPowerShell()
    {
        var psi = ProcessLaunch.BuildStartInfo(@"C:\scoop\shims\scoop.ps1", new[] { "update", "dotnet-6502" }, isWindows: true);

        Assert.Equal("powershell.exe", psi.FileName);
        Assert.Contains("-File", psi.ArgumentList);
        Assert.Contains(@"C:\scoop\shims\scoop.ps1", psi.ArgumentList);
        Assert.Contains("update", psi.ArgumentList);
    }

    [Fact]
    public void Windows_Ps1Shim_UsesGivenPowerShellHost()
    {
        var psi = ProcessLaunch.BuildStartInfo(@"C:\scoop\shims\scoop.ps1", new[] { "update", "x" }, isWindows: true, powerShellExe: "pwsh.exe");

        Assert.Equal("pwsh.exe", psi.FileName);
        Assert.Contains("-File", psi.ArgumentList);
    }

    [Fact]
    public void ResolveWindowsPowerShellExe_IsWindowsPowerShell_OffWindows()
    {
        // Off Windows the host is irrelevant; must return the always-present powershell.exe (no probing).
        if (OperatingSystem.IsWindows())
            return;
        Assert.Equal("powershell.exe", ProcessLaunch.ResolveWindowsPowerShellExe());
    }

    [Fact]
    public void Windows_Exe_IsRunDirectly()
    {
        var psi = ProcessLaunch.BuildStartInfo(@"C:\tools\brew.exe", new[] { "upgrade", "x" }, isWindows: true);

        Assert.Equal(@"C:\tools\brew.exe", psi.FileName);
        Assert.Equal(new[] { "upgrade", "x" }, psi.ArgumentList);
    }

    [Fact]
    public void Unix_ManagerPath_IsRunDirectly()
    {
        // brew has no extension; on Unix nothing is wrapped even for a .cmd-looking name.
        var psi = ProcessLaunch.BuildStartInfo("/opt/homebrew/bin/brew", new[] { "list", "--versions", "dotnet-6502-terminal" }, isWindows: false);

        Assert.Equal("/opt/homebrew/bin/brew", psi.FileName);
        Assert.Equal(new[] { "list", "--versions", "dotnet-6502-terminal" }, psi.ArgumentList);
    }
}
