using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Highbyte.DotNet6502.Updates;

/// <summary>Production <see cref="IInstallChannelProbe"/> backed by the real OS, filesystem, and processes.</summary>
public sealed class SystemInstallChannelProbe : IInstallChannelProbe
{
    public static readonly SystemInstallChannelProbe Instance = new();

    public OSPlatformKind OS
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OSPlatformKind.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OSPlatformKind.MacOS;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return OSPlatformKind.Linux;
            return OSPlatformKind.Other;
        }
    }

    public string BaseDirectory => AppContext.BaseDirectory;

    public string? HomeDirectory
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrWhiteSpace(home) ? Environment.GetEnvironmentVariable("HOME") : home;
        }
    }

    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string? ReadFileFirstLine(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                    return trimmed;
            }
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public string? ResolveExecutable(string command, IEnumerable<string> preferredDirectories)
    {
        var isWindows = OS == OSPlatformKind.Windows;
        // Prefer a native .exe, then the PowerShell shim (.ps1) — Scoop is a PowerShell tool and its
        // .cmd shim is just a trampoline to scoop.ps1, so running the .ps1 directly is more direct on
        // modern Windows. Fall back to .cmd/.bat/bare for robustness.
        var candidateNames = isWindows
            ? new[] { command + ".exe", command + ".ps1", command + ".cmd", command + ".bat", command }
            : new[] { command };

        foreach (var dir in preferredDirectories)
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;
            foreach (var name in candidateNames)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var name in candidateNames)
                {
                    var candidate = Path.Combine(dir, name);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
        }

        return null;
    }

    public ProcessRunResult? RunCommand(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        try
        {
            // Launch .ps1/.cmd/.bat (e.g. the Scoop shim scoop.ps1) correctly on Windows, preferring pwsh 7.
            var startInfo = ProcessLaunch.BuildStartInfo(
                executablePath, arguments, OS == OSPlatformKind.Windows, ProcessLaunch.ResolveWindowsPowerShellExe());
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            var stdout = process.StandardOutput.ReadToEnd();
            // Drain stderr too so a chatty manager can't fill the pipe and deadlock.
            _ = process.StandardError.ReadToEnd();

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return null;
            }

            return new ProcessRunResult(process.ExitCode, stdout);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return null;
        }
    }
}
