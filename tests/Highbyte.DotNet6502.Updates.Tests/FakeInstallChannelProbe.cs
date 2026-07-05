using Highbyte.DotNet6502.Updates;

namespace Highbyte.DotNet6502.Updates.Tests;

/// <summary>In-memory <see cref="IInstallChannelProbe"/> for exercising <see cref="InstallChannelDetector"/>.</summary>
internal sealed class FakeInstallChannelProbe : IInstallChannelProbe
{
    public OSPlatformKind OS { get; set; } = OSPlatformKind.MacOS;
    public string BaseDirectory { get; set; } = "/app";
    public string? HomeDirectory { get; set; } = "/home/user";

    public Dictionary<string, string?> EnvironmentVariables { get; } = new();
    public Dictionary<string, string> Files { get; } = new();           // path -> first line
    public HashSet<string> Directories { get; } = new();
    public Dictionary<string, string> ResolvableExecutables { get; } = new(); // command -> full path

    /// <summary>(executablePath, joined args) -> result. Missing entry ⇒ command could not be launched (null).</summary>
    public Dictionary<string, ProcessRunResult> CommandResults { get; } = new();

    public string? GetEnvironmentVariable(string name)
        => EnvironmentVariables.TryGetValue(name, out var v) ? v : null;

    public bool DirectoryExists(string path)
        => Directories.Contains(path)
           || Directories.Any(x => PathEquals(x, path));

    public string? ReadFileFirstLine(string path)
    {
        if (Files.TryGetValue(path, out var value))
            return value;

        var matchingPath = Files.Keys.FirstOrDefault(x => PathEquals(x, path));
        return matchingPath is null ? null : Files[matchingPath];
    }

    public string? ResolveExecutable(string command, IEnumerable<string> preferredDirectories)
        => ResolvableExecutables.TryGetValue(command, out var path) ? path : null;

    public ProcessRunResult? RunCommand(string executablePath, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var key = executablePath + " " + string.Join(' ', arguments);
        return CommandResults.TryGetValue(key, out var result) ? result : null;
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.Ordinal);

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');
}
