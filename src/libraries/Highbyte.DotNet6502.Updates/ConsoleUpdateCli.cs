namespace Highbyte.DotNet6502.Updates;

/// <summary>
/// Console-host glue for the update flow: the <c>--version</c> / <c>--check-update</c> / <c>--update</c>
/// flags, and a quiet, gated one-line "update available" startup notice. Kept host-agnostic by writing
/// to caller-supplied <see cref="TextWriter"/>s; suppression follows the npm <c>update-notifier</c>
/// conventions (skip when non-interactive, in CI, or opted out).
/// </summary>
public static class ConsoleUpdateCli
{
    public const string OptOutEnvVar = "DOTNET6502_NO_UPDATE_CHECK";

    private static readonly string[] HandledFlags = { "--version", "--check-update", "--update" };

    /// <summary>True when args request the version or an explicit update check, so the host can early-return.</summary>
    public static bool WantsHandling(string[] args) => args.Any(a => HandledFlags.Contains(a, StringComparer.Ordinal));

    /// <summary>
    /// Handles <c>--version</c> / <c>--check-update</c> / <c>--update</c> (explicit, so output goes to
    /// <paramref name="output"/> = stdout) and returns the process exit code. Assumes
    /// <see cref="WantsHandling"/> was true.
    /// </summary>
    public static async Task<int> RunAsync(string[] args, AppUpdateDescriptor descriptor, TextWriter output, CancellationToken cancellationToken = default)
    {
        if (args.Contains("--version", StringComparer.Ordinal))
        {
            var current = AppVersion.GetCurrent();
            output.WriteLine(current is null ? "unknown (development build)" : $"v{current}");
            return 0;
        }

        UpdateCheckResult result;
        try
        {
            using var http = CreateHttpClient();
            var checker = UpdateChecker.CreateDefault(descriptor, http);
            result = await checker.CheckAsync(new UpdateCheckContext { ForceCheck = true }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            output.WriteLine($"Update check failed: {ex.Message}");
            return 0;
        }

        PrintVerbose(result, output);
        return 0;
    }

    /// <summary>
    /// Prints a single "update available" line to <paramref name="errorOutput"/> (stderr, so it never
    /// pollutes machine-readable stdout) when appropriate. No-op when suppressed, not managed, or up to
    /// date. Respects the daily cadence cache and never throws — a failed notice must not break startup.
    /// </summary>
    public static async Task NotifyOnStartupAsync(AppUpdateDescriptor descriptor, TextWriter errorOutput, CancellationToken cancellationToken = default)
    {
        if (IsSuppressed())
            return;

        try
        {
            using var http = CreateHttpClient();
            var checker = UpdateChecker.CreateDefault(descriptor, http);
            var result = await checker.CheckAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result.IsUpdateAvailable)
                errorOutput.WriteLine(FormatNoticeLine(result));
        }
        catch
        {
            // Best effort: never let the update notice break or delay-fail the host.
        }
    }

    /// <summary>Standard suppressors: opt-out env var, CI, or a non-interactive (redirected) stderr.</summary>
    public static bool IsSuppressed()
    {
        if (IsEnvFlagSet(OptOutEnvVar))
            return true;
        if (IsEnvFlagSet("CI"))
            return true;
        // The notice is written to stderr; if that's redirected we're likely scripted/piped.
        if (Console.IsErrorRedirected)
            return true;
        return false;
    }

    private static bool IsEnvFlagSet(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return !string.IsNullOrEmpty(value)
            && !value.Equals("0", StringComparison.Ordinal)
            && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The one-line "update available" message shared by the startup notice and verbose output.</summary>
    public static string FormatNoticeLine(UpdateCheckResult result)
        => $"A newer version v{result.LatestVersion} is available (you have v{result.CurrentVersion}). Run '{result.SuggestedCommand}' to update.";

    private static void PrintVerbose(UpdateCheckResult result, TextWriter output)
    {
        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable:
                output.WriteLine(FormatNoticeLine(result));
                if (!string.IsNullOrEmpty(result.ReleaseNotesUrl))
                    output.WriteLine($"Release notes: {result.ReleaseNotesUrl}");
                output.WriteLine("(Automatic in-app update isn't available yet — run the command above to update.)");
                break;
            case UpdateCheckStatus.UpToDate:
                output.WriteLine($"You're on the latest version (v{result.CurrentVersion}).");
                break;
            case UpdateCheckStatus.NotManaged:
                output.Write("This build isn't installed via Homebrew or Scoop, so there's no update check. ");
                output.WriteLine(result.CurrentVersion is null
                    ? "Version: unknown (development build)."
                    : $"Current version: v{result.CurrentVersion}.");
                break;
            case UpdateCheckStatus.VersionUnknown:
                output.WriteLine("Development build (version unknown); skipping update check.");
                break;
            case UpdateCheckStatus.Error:
                output.WriteLine($"Update check failed: {result.Error}");
                break;
        }
    }

    private static HttpClient CreateHttpClient() => new() { Timeout = TimeSpan.FromSeconds(5) };
}
