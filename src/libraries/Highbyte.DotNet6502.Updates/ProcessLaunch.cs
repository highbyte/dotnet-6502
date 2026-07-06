using System.Diagnostics;

namespace Highbyte.DotNet6502.Updates;

/// <summary>
/// Builds a <see cref="ProcessStartInfo"/> that launches an executable OR a Windows shell script
/// correctly. This matters because Scoop is a PowerShell tool: the preferred shim is <c>scoop.ps1</c>
/// (Scoop's <c>scoop.cmd</c> is just a trampoline to it). Scripts can't be started directly with
/// <c>UseShellExecute=false</c> — a <c>.ps1</c> is run via a PowerShell host, and a <c>.cmd</c>/<c>.bat</c>
/// via <c>cmd.exe /c</c>. For the PowerShell host it prefers <c>pwsh.exe</c> (PowerShell 7) when present
/// and falls back to the always-installed <c>powershell.exe</c> (Windows PowerShell 5.1) — the same
/// choice Scoop's own <c>scoop.cmd</c> makes. Homebrew's <c>brew</c> is a plain executable, so on Unix
/// (and for real .exe files) this is a straight pass-through.
/// </summary>
public static class ProcessLaunch
{
    public static ProcessStartInfo BuildStartInfo(
        string executablePath,
        IReadOnlyList<string> args,
        bool isWindows,
        string powerShellExe = "powershell.exe")
    {
        var psi = new ProcessStartInfo { UseShellExecute = false, CreateNoWindow = true };

        var extension = isWindows ? Path.GetExtension(executablePath).ToLowerInvariant() : string.Empty;
        switch (extension)
        {
            case ".cmd":
            case ".bat":
                psi.FileName = "cmd.exe";
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(executablePath);
                break;
            case ".ps1":
                psi.FileName = powerShellExe;
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-NonInteractive");
                psi.ArgumentList.Add("-ExecutionPolicy");
                psi.ArgumentList.Add("Bypass");
                psi.ArgumentList.Add("-File");
                psi.ArgumentList.Add(executablePath);
                break;
            default:
                psi.FileName = executablePath;
                break;
        }

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        return psi;
    }

    /// <summary>
    /// The PowerShell host to run <c>.ps1</c> scripts with: <c>pwsh.exe</c> (PowerShell 7) when it's on
    /// PATH, else <c>powershell.exe</c> (Windows PowerShell 5.1, always present). Always
    /// <c>powershell.exe</c> off Windows (irrelevant there). Mirrors Scoop's <c>scoop.cmd</c>.
    /// </summary>
    public static string ResolveWindowsPowerShellExe()
    {
        if (!OperatingSystem.IsWindows())
            return "powershell.exe";
        return IsExecutableOnPath("pwsh.exe") ? "pwsh.exe" : "powershell.exe";
    }

    private static bool IsExecutableOnPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return false;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(dir, fileName)))
                    return true;
            }
            catch (ArgumentException)
            {
                // A malformed PATH entry — skip it.
            }
        }
        return false;
    }
}
