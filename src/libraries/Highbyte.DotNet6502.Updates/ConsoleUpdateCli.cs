using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Updates;

/// <summary>
/// Console-host glue for the update flow: the explicit <c>--version</c> / <c>--check-update</c> /
/// <c>--update</c> flags (output to a caller-supplied <see cref="TextWriter"/> = stdout), and a gated
/// automatic startup check that logs an "update available" line via <see cref="ILogger"/> (routing is
/// the host's concern). Suppression of the automatic check follows the npm <c>update-notifier</c>
/// conventions (skip in CI or when opted out).
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

        // --update: actually run the package-manager upgrade in the foreground (this invocation is a
        // short-lived flag handler, not the running app, so no quit/relaunch is needed).
        if (args.Contains("--update", StringComparer.Ordinal)
            && result.Status == UpdateCheckStatus.UpdateAvailable
            && result.ManagerExecutablePath is not null)
        {
            output.WriteLine(FormatNoticeLine(result));
            output.WriteLine($"Updating: {result.SuggestedCommand}");
            output.WriteLine();
            var succeeded = await UpdateApplier.RunUpgradeAsync(
                result.ManagerExecutablePath, descriptor.UpgradeArgs(result.Channel), output, cancellationToken).ConfigureAwait(false);
            output.WriteLine();
            output.WriteLine(succeeded
                ? "Update complete. Restart the app to use the new version."
                : "Update failed. See the output above; you can run the command manually.");
            return succeeded ? 0 : 1;
        }

        PrintVerbose(result, output);
        return 0;
    }

    /// <summary>
    /// Runs the automatic (cadence-cached) startup update check and, if an update is available, logs one
    /// <see cref="LogLevel.Information"/> line via <paramref name="logger"/> (the checker emits it — the
    /// shared log point; the host decides where it's routed). Gated: a no-op when
    /// <paramref name="updateCheckEnabled"/> is false or the environment suppresses it (CI / opt-out env
    /// var). Never throws — a failed startup check must not disrupt the app.
    /// </summary>
    public static async Task CheckAndLogOnStartupAsync(
        AppUpdateDescriptor descriptor,
        ILogger logger,
        bool updateCheckEnabled,
        CancellationToken cancellationToken = default)
    {
        if (!updateCheckEnabled || IsSuppressedByEnvironment())
            return;

        try
        {
            using var http = CreateHttpClient();
            var checker = UpdateChecker.CreateDefault(descriptor, http, logger);
            await checker.CheckAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best effort: never let the update check break or delay-fail startup.
        }
    }

    /// <summary>
    /// Environmental suppressors for the <em>automatic</em> check: the opt-out env var
    /// (<see cref="OptOutEnvVar"/>) or a CI environment. Explicit flags (<c>--check-update</c>) ignore these.
    /// </summary>
    public static bool IsSuppressedByEnvironment()
        => IsEnvFlagSet(OptOutEnvVar) || IsEnvFlagSet("CI");

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
                output.WriteLine("(Run with --update to upgrade automatically, or run the command above yourself.)");
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
