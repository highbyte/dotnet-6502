using System.Diagnostics;
using Highbyte.DotNet6502.Updates;
using Xunit;

namespace Highbyte.DotNet6502.Updates.Tests;

public class UpdateApplierTests
{
    /// <summary>
    /// The detached relauncher's real Unix path, end-to-end: it must wait for the target process to
    /// exit, then run the upgrade, then relaunch. (The Windows PowerShell path can only run on Windows.)
    /// </summary>
    [Fact]
    public async Task TrySpawnDetachedRelaunch_WaitsForApp_RunsUpgrade_ThenRelaunches()
    {
        if (OperatingSystem.IsWindows())
            return;

        var (ranBeforeExit, upgraded, relaunched) = await RunDetachedScenarioAsync(managerExitCode: 0);

        Assert.False(ranBeforeExit, "upgrade ran before the app exited");
        Assert.True(upgraded, "upgrade did not run");
        Assert.True(relaunched, "relaunch did not run");
    }

    /// <summary>
    /// Fail-safe: if the upgrade fails (manager exits non-zero), the helper must STILL relaunch — so the
    /// user is left with the (old) app running, not nothing.
    /// </summary>
    [Fact]
    public async Task TrySpawnDetachedRelaunch_StillRelaunches_WhenUpgradeFails()
    {
        if (OperatingSystem.IsWindows())
            return;

        var (_, upgraded, relaunched) = await RunDetachedScenarioAsync(managerExitCode: 1);

        Assert.True(upgraded, "upgrade step did not run");
        Assert.True(relaunched, "relaunch did not run after a failed upgrade (fail-safe broken)");
    }

    [Fact]
    public async Task TrySpawnDetachedRelaunch_RunsUpgradeCommands_InOrder()
    {
        if (OperatingSystem.IsWindows())
            return;

        var dir = Path.Combine(Path.GetTempPath(), "d6502-relaunch-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Process? app = null;
        try
        {
            var log = Path.Combine(dir, "commands.log");
            var relaunchMarker = Path.Combine(dir, "relaunched");
            var manager = Path.Combine(dir, "manager.sh");
            await File.WriteAllTextAsync(manager, $"#!/bin/bash\necho \"$*\" >> '{log}'\nexit 0\n");
            var relaunchScript = Path.Combine(dir, "relaunch.sh");
            await File.WriteAllTextAsync(relaunchScript, $"#!/bin/bash\ntouch '{relaunchMarker}'\n");
            MakeExecutable(manager);
            MakeExecutable(relaunchScript);

            app = Process.Start(new ProcessStartInfo { FileName = "/bin/bash", UseShellExecute = false }
                .WithArgs("-c", "sleep 30"))!;

            var spawned = UpdateApplier.TrySpawnDetachedRelaunch(
                manager,
                new IReadOnlyList<string>[] { new[] { "update" }, new[] { "upgrade", "--no-ask", "--cask", "dotnet-6502" } },
                app.Id,
                new RelaunchSpec("/bin/bash", new[] { relaunchScript }),
                logFilePath: Path.Combine(dir, "update.log"));
            Assert.True(spawned, "helper was not spawned");

            app.Kill(entireProcessTree: true);
            await app.WaitForExitAsync();

            Assert.True(await WaitForFileAsync(relaunchMarker, TimeSpan.FromSeconds(15)), "relaunch did not run");
            var commandLogAppeared = await WaitForFileAsync(log, TimeSpan.FromSeconds(5));
            Assert.True(commandLogAppeared, "manager command log was not written");
            Assert.Equal(new[] { "update", "upgrade --no-ask --cask dotnet-6502" }, await File.ReadAllLinesAsync(log));
        }
        finally
        {
            try { if (app is { HasExited: false }) app.Kill(entireProcessTree: true); } catch { /* best effort */ }
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task TrySpawnDetachedRelaunch_LogsUnixRelaunchCommandAndFailure()
    {
        if (OperatingSystem.IsWindows())
            return;

        var dir = Path.Combine(Path.GetTempPath(), "d6502-relaunch-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Process? app = null;
        try
        {
            var manager = Path.Combine(dir, "manager.sh");
            await File.WriteAllTextAsync(manager, "#!/bin/bash\nexit 0\n");
            MakeExecutable(manager);

            app = Process.Start(new ProcessStartInfo { FileName = "/bin/bash", UseShellExecute = false }
                .WithArgs("-c", "sleep 30"))!;

            var logPath = Path.Combine(dir, "update.log");
            var missingLauncher = Path.Combine(dir, "missing-dotnet-6502");
            var spawned = UpdateApplier.TrySpawnDetachedRelaunch(
                manager,
                Array.Empty<string>(),
                app.Id,
                new RelaunchSpec(missingLauncher, new[] { "--console-log" }),
                logFilePath: logPath);
            Assert.True(spawned, "helper was not spawned");

            app.Kill(entireProcessTree: true);
            await app.WaitForExitAsync();

            Assert.True(
                await WaitForFileContainingAsync(logPath, "Relaunching:", TimeSpan.FromSeconds(15)),
                "relaunch command was not logged");
            Assert.True(
                await WaitForFileContainingAsync(logPath, "Relaunch command failed with exit code", TimeSpan.FromSeconds(5)),
                "relaunch failure was not logged");
            var log = await File.ReadAllTextAsync(logPath);
            Assert.Contains(missingLauncher, log);
        }
        finally
        {
            try { if (app is { HasExited: false }) app.Kill(entireProcessTree: true); } catch { /* best effort */ }
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    // Runs the full detached scenario with a fake manager that touches a marker then exits with the
    // given code, and a fake relaunch that touches another marker. Returns whether the upgrade ran
    // before the app exited (should be false), and whether the upgrade + relaunch markers appeared.
    private static async Task<(bool ranBeforeExit, bool upgraded, bool relaunched)> RunDetachedScenarioAsync(int managerExitCode)
    {
        var dir = Path.Combine(Path.GetTempPath(), "d6502-relaunch-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Process? app = null;
        try
        {
            var upgradeMarker = Path.Combine(dir, "upgraded");
            var relaunchMarker = Path.Combine(dir, "relaunched");

            var manager = Path.Combine(dir, "manager.sh");
            await File.WriteAllTextAsync(manager, $"#!/bin/bash\ntouch '{upgradeMarker}'\nexit {managerExitCode}\n");
            var relaunchScript = Path.Combine(dir, "relaunch.sh");
            await File.WriteAllTextAsync(relaunchScript, $"#!/bin/bash\ntouch '{relaunchMarker}'\n");
            MakeExecutable(manager);
            MakeExecutable(relaunchScript);

            app = Process.Start(new ProcessStartInfo { FileName = "/bin/bash", UseShellExecute = false }
                .WithArgs("-c", "sleep 30"))!;

            var spawned = UpdateApplier.TrySpawnDetachedRelaunch(
                manager,
                Array.Empty<string>(),
                app.Id,
                new RelaunchSpec("/bin/bash", new[] { relaunchScript }),
                logFilePath: Path.Combine(dir, "update.log")); // keep test output out of the real cache
            Assert.True(spawned, "helper was not spawned");

            // The helper should still be waiting for the app to exit.
            await Task.Delay(500);
            var ranBeforeExit = File.Exists(upgradeMarker);

            app.Kill(entireProcessTree: true);
            await app.WaitForExitAsync();

            var relaunched = await WaitForFileAsync(relaunchMarker, TimeSpan.FromSeconds(15));
            return (ranBeforeExit, File.Exists(upgradeMarker), relaunched);
        }
        finally
        {
            try { if (app is { HasExited: false }) app.Kill(entireProcessTree: true); } catch { /* best effort */ }
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }

    private static async Task<bool> WaitForFileAsync(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
                return true;
            await Task.Delay(100);
        }
        return File.Exists(path);
    }

    private static async Task<bool> WaitForFileContainingAsync(string path, string text, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path) && (await File.ReadAllTextAsync(path)).Contains(text, StringComparison.Ordinal))
                return true;
            await Task.Delay(100);
        }
        return File.Exists(path) && (await File.ReadAllTextAsync(path)).Contains(text, StringComparison.Ordinal);
    }
}

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo WithArgs(this ProcessStartInfo psi, params string[] args)
    {
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        return psi;
    }
}
