namespace Highbyte.DotNet6502.Updates;

public enum OSPlatformKind
{
    Other = 0,
    Windows,
    MacOS,
    Linux,
}

/// <summary>Outcome of running an external command (package-manager query).</summary>
public sealed record ProcessRunResult(int ExitCode, string StandardOutput);

/// <summary>
/// The OS / filesystem / process operations <see cref="InstallChannelDetector"/> needs. Abstracted
/// so channel detection — which shells out to <c>brew</c>/<c>scoop</c> and reads marker files — can
/// be unit-tested with a fake, and driven for real by <see cref="SystemInstallChannelProbe"/>.
/// </summary>
public interface IInstallChannelProbe
{
    OSPlatformKind OS { get; }

    /// <summary>Directory the app runs from (<see cref="AppContext.BaseDirectory"/> in production).</summary>
    string BaseDirectory { get; }

    string? HomeDirectory { get; }

    string? GetEnvironmentVariable(string name);

    bool DirectoryExists(string path);

    /// <summary>First non-empty trimmed line of the file, or null if the file is missing/empty.</summary>
    string? ReadFileFirstLine(string path);

    /// <summary>
    /// Resolves the full path to <paramref name="command"/>, checking <paramref name="preferredDirectories"/>
    /// first (GUI apps don't inherit the shell PATH) and then PATH. Returns null if not found.
    /// </summary>
    string? ResolveExecutable(string command, IEnumerable<string> preferredDirectories);

    /// <summary>Runs a command and captures its exit code + stdout; returns null if it couldn't be launched.</summary>
    ProcessRunResult? RunCommand(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout);
}
