using Highbyte.DotNet6502.Updates;

namespace Highbyte.DotNet6502.App.RemoteClient;

/// <summary>
/// Pure evaluation for the <c>--check-server-version</c> preflight: compares the remote endpoint's
/// release-stamped app version with this client's own version and produces the user-facing messages
/// and process exit code. No I/O here so it can be unit-tested; <c>Program</c> does the TCP round-trip
/// and prints the returned lines.
/// </summary>
internal static class ServerVersionCheck
{
    /// <summary>Distinct exit code for a version mismatch (0 = ok / can't-verify, 1 = connection error, 2 = bad args).</summary>
    public const int MismatchExitCode = 3;

    public sealed record Result(int ExitCode, IReadOnlyList<string> StdoutLines, IReadOnlyList<string> StderrLines);

    /// <summary>
    /// The remote client's own package name is fixed; the server package depends on which host app
    /// answered (its <c>HostName</c>).
    /// </summary>
    private const string ClientPackageHint =
        "dotnet-6502-remote (e.g. 'brew upgrade dotnet-6502-remote' or 'scoop update dotnet-6502-remote')";

    private static string ServerPackageHint(string? serverApp) => serverApp switch
    {
        "Avalonia" => "dotnet-6502 (e.g. 'brew upgrade --cask dotnet-6502' on macOS, 'brew upgrade dotnet-6502' on Linux, or 'scoop update dotnet-6502')",
        "Headless" => "dotnet-6502-headless (e.g. 'brew upgrade dotnet-6502-headless' or 'scoop update dotnet-6502-headless')",
        _ => "the server app's package",
    };

    private static string Describe(string? serverApp) => string.IsNullOrEmpty(serverApp) ? "remote endpoint" : $"remote endpoint ({serverApp})";

    private static string Show(SemanticVersion? v) => v is null ? "unknown" : $"v{v}";

    /// <summary>
    /// The server did not recognize the <c>server.info</c> command — it predates this feature, so it is
    /// definitely older than the client. Treated as a mismatch.
    /// </summary>
    public static Result ServerTooOld(string? serverApp) => new(
        MismatchExitCode,
        StdoutLines: Array.Empty<string>(),
        StderrLines: new[]
        {
            $"Warning: the {Describe(serverApp)} does not support the version check (it predates this feature), so it is older than dotnet-6502-remote.",
            "Update the server app to the latest package-manager version:",
            $"  Server:       {ServerPackageHint(serverApp)}",
        });

    /// <summary>
    /// Compares the server's (already raw-parsed) version with the client's. Either being null means a
    /// development/unstamped build that can't be compared → "can't verify" (exit 0). Equal → ok (exit 0).
    /// Different → warning with update hints (exit <see cref="MismatchExitCode"/>).
    /// </summary>
    public static Result Evaluate(string? serverApp, SemanticVersion? serverVersion, SemanticVersion? clientVersion)
    {
        if (serverVersion is null || clientVersion is null)
        {
            var which = serverVersion is null && clientVersion is null
                ? "both the server app and dotnet-6502-remote are development builds"
                : serverVersion is null
                    ? "the server app is a development build (or too old to report a version)"
                    : "dotnet-6502-remote is a development build";
            return new Result(
                0,
                StdoutLines: Array.Empty<string>(),
                StderrLines: new[] { $"Cannot verify versions: {which}; development builds are unversioned." });
        }

        if (serverVersion.Equals(clientVersion))
        {
            return new Result(
                0,
                StdoutLines: new[] { $"OK: {Describe(serverApp)} {Show(serverVersion)} matches dotnet-6502-remote {Show(clientVersion)}." },
                StderrLines: Array.Empty<string>());
        }

        var direction = serverVersion > clientVersion ? "newer than" : "older than";
        return new Result(
            MismatchExitCode,
            StdoutLines: Array.Empty<string>(),
            StderrLines: new[]
            {
                $"Warning: the {Describe(serverApp)} is {Show(serverVersion)} ({direction} dotnet-6502-remote {Show(clientVersion)}).",
                "Bring both sides to the latest package-manager version:",
                $"  RemoteClient: {ClientPackageHint}",
                $"  Server:       {ServerPackageHint(serverApp)}",
                "Note: Homebrew/Scoop normally offer only the latest version, so update both rather than trying to match an older one.",
            });
    }
}
