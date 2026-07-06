using System.Diagnostics;
using System.Text;
using Highbyte.DotNet6502.Systems.Configuration;

namespace Highbyte.DotNet6502.Updates;

/// <summary>How to relaunch the app after the upgrade: an executable plus its arguments.</summary>
public sealed record RelaunchSpec(string Executable, IReadOnlyList<string> Arguments);

/// <summary>
/// Applies a delegated update by running the package manager (Phase B).
///
/// <see cref="RunUpgradeAsync"/> is the foreground path used by the console <c>--update</c> flag: that
/// invocation is a short-lived flag handler, not the running app, so it can upgrade in place and just
/// report — no quit/PID-wait/relaunch. The GUI's one-click button (where the <em>running</em> app
/// updates itself) needs the detached wait-PID → upgrade → relaunch helper instead.
/// </summary>
public static class UpdateApplier
{
    /// <summary>
    /// Runs <c>&lt;managerExecutablePath&gt; &lt;args&gt;</c> in the foreground, streaming its stdout and
    /// stderr to <paramref name="output"/>. Returns true on exit code 0. Never throws for the expected
    /// launch failures.
    /// </summary>
    public static async Task<bool> RunUpgradeAsync(
        string managerExecutablePath,
        IReadOnlyList<string> args,
        TextWriter output,
        CancellationToken cancellationToken = default)
        => await RunUpgradeCommandsAsync(managerExecutablePath, new[] { args }, output, cancellationToken).ConfigureAwait(false);

    public static async Task<bool> RunUpgradeCommandsAsync(
        string managerExecutablePath,
        IReadOnlyList<IReadOnlyList<string>> commandArgs,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        foreach (var args in commandArgs)
        {
            // Launch .ps1/.cmd/.bat (e.g. the Scoop shim scoop.ps1) correctly on Windows, preferring pwsh 7.
            var startInfo = ProcessLaunch.BuildStartInfo(
                managerExecutablePath, args, OperatingSystem.IsWindows(), ProcessLaunch.ResolveWindowsPowerShellExe());
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            Process? process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
            {
                output.WriteLine($"Could not start the package manager: {ex.Message}");
                return false;
            }

            if (process is null)
            {
                output.WriteLine("Could not start the package manager.");
                return false;
            }

            using (process)
            {
                process.OutputDataReceived += (_, e) => { if (e.Data != null) output.WriteLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.WriteLine(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                if (process.ExitCode != 0)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Spawns a DETACHED helper that waits for the app (<paramref name="appProcessId"/>) to exit, runs
    /// the upgrade, then relaunches — the new version, or the old one if the upgrade failed (fail-safe).
    /// For the GUI's one-click update: the caller spawns this and then quits the app. Returns false if
    /// the helper couldn't be spawned (caller should fall back to showing the command). Not testable
    /// without a real brew/scoop install.
    /// </summary>
    /// <summary>Log file the detached helper appends its upgrade output to, so a GUI update outcome (incl. failures) isn't lost.</summary>
    public static string GetUpdateLogPath()
        => Path.Combine(AppStoragePaths.GetCacheRoot(), "updates", "last-update.log");

    public static bool TrySpawnDetachedRelaunch(
        string managerExecutablePath,
        IReadOnlyList<string> upgradeArgs,
        int appProcessId,
        RelaunchSpec relaunch,
        string? logFilePath = null)
        => TrySpawnDetachedRelaunch(
            managerExecutablePath, new[] { upgradeArgs }, appProcessId, relaunch, logFilePath);

    public static bool TrySpawnDetachedRelaunch(
        string managerExecutablePath,
        IReadOnlyList<IReadOnlyList<string>> upgradeCommandArgs,
        int appProcessId,
        RelaunchSpec relaunch,
        string? logFilePath = null)
    {
        var logPath = logFilePath ?? GetUpdateLogPath();
        try
        {
            return OperatingSystem.IsWindows()
                ? TrySpawnDetachedWindows(managerExecutablePath, upgradeCommandArgs, appProcessId, relaunch, logPath)
                : TrySpawnDetachedUnix(managerExecutablePath, upgradeCommandArgs, appProcessId, relaunch, logPath);
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    // Unix (macOS/Linux): a bash script waits on the PID, upgrades, then relaunches. The app spawns
    // /bin/bash and exits; the child reparents to init and survives (GUI apps have no controlling
    // terminal). No elevation needed — brew installs to user-writable locations.
    private static bool TrySpawnDetachedUnix(string manager, IReadOnlyList<IReadOnlyList<string>> upgradeCommandArgs, int pid, RelaunchSpec relaunch, string logPath)
    {
        var script = new StringBuilder();
        script.Append("#!/bin/bash\n");
        // Capture everything (incl. the upgrade output/errors) so a failed update isn't lost.
        script.Append("LOG=").Append(ShQuote(logPath)).Append('\n');
        script.Append("mkdir -p \"$(dirname \"$LOG\")\" 2>/dev/null\n");
        script.Append("exec >>\"$LOG\" 2>&1\n");
        script.Append("echo \"=== update $(date) ===\"\n");
        script.Append("APP_PID=\"$1\"\n");
        script.Append("while kill -0 \"$APP_PID\" 2>/dev/null; do sleep 0.3; done\n");
        // Upgrade; fail-safe means we relaunch regardless of the outcome.
        script.Append("STATUS=0\n");
        foreach (var args in upgradeCommandArgs)
        {
            script.Append("if [ \"$STATUS\" -eq 0 ]; then\n");
            script.Append("  ").Append(ShJoin(manager, args)).Append('\n');
            script.Append("  STATUS=$?\n");
            script.Append("  if [ \"$STATUS\" -ne 0 ]; then echo \"Package manager command failed with exit code $STATUS\"; fi\n");
            script.Append("fi\n");
        }
        // Relaunch detached from this helper.
        script.Append(ShJoin(relaunch.Executable, relaunch.Arguments)).Append(" &\n");
        script.Append("exit 0\n");

        var scriptPath = WriteTempScript(script.ToString(), ".sh");

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(pid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Process.Start(psi) != null;
    }

    // Windows: a PowerShell script waits on the PID, upgrades, then relaunches. Launched via
    // ShellExecute (UseShellExecute=true) so it isn't tied to the exiting app's lifetime.
    private static bool TrySpawnDetachedWindows(string manager, IReadOnlyList<IReadOnlyList<string>> upgradeCommandArgs, int pid, RelaunchSpec relaunch, string logPath)
    {
        var script = new StringBuilder();
        script.Append("param([int]$AppPid)\n");
        script.Append("$log = ").Append(PsQuote(logPath)).Append('\n');
        script.Append("New-Item -ItemType Directory -Force -Path (Split-Path $log) *> $null\n");
        script.Append("\"=== update $(Get-Date) ===\" | Out-File -FilePath $log -Append\n");
        script.Append("try { Wait-Process -Id $AppPid -ErrorAction SilentlyContinue } catch {}\n");
        // Capture the upgrade output/errors (all streams) so a failed update isn't lost.
        script.Append("$status = 0\n");
        foreach (var args in upgradeCommandArgs)
        {
            script.Append("if ($status -eq 0) {\n");
            script.Append("  try { & ").Append(PsQuote(manager));
            foreach (var a in args)
                script.Append(' ').Append(PsQuote(a));
            script.Append(" *>> $log; $status = $LASTEXITCODE } catch { $_ | Out-File -FilePath $log -Append; $status = 1 }\n");
            script.Append("  if ($status -ne 0) { \"Package manager command failed with exit code $status\" | Out-File -FilePath $log -Append }\n");
            script.Append("}\n");
        }
        script.Append("Start-Process -FilePath ").Append(PsQuote(relaunch.Executable));
        if (relaunch.Arguments.Count > 0)
        {
            script.Append(" -ArgumentList ");
            script.Append(string.Join(",", relaunch.Arguments.Select(PsQuote)));
        }
        script.Append('\n');

        var scriptPath = WriteTempScript(script.ToString(), ".ps1");

        var psi = new ProcessStartInfo
        {
            FileName = ProcessLaunch.ResolveWindowsPowerShellExe(), // pwsh 7 if present, else Windows PowerShell 5.1
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            // Run the helper from a neutral directory, NOT the app's inherited working directory. A
            // Scoop-installed GUI app's working directory is its own install dir (...\apps\<pkg>\current);
            // if the helper inherits that, Scoop's `scoop update` can't remove/repoint the `current`
            // junction ("Cannot remove the item ... because it is in use") because this process is sitting
            // in it. TempPath is always writable and is never the package dir being replaced.
            WorkingDirectory = Path.GetTempPath(),
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-WindowStyle");
        psi.ArgumentList.Add("Hidden");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(pid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Process.Start(psi) != null;
    }

    private static string WriteTempScript(string content, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dotnet6502-update-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, content);
        return path;
    }

    private static string ShJoin(string exe, IReadOnlyList<string> args)
    {
        var sb = new StringBuilder(ShQuote(exe));
        foreach (var a in args)
            sb.Append(' ').Append(ShQuote(a));
        return sb.ToString();
    }

    private static string ShQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";

    private static string PsQuote(string s) => "'" + s.Replace("'", "''") + "'";
}
