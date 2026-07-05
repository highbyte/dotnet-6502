using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Services;

/// <summary>
/// Result of an update check, in terms the UI needs — deliberately free of any
/// <c>Highbyte.DotNet6502.Updates</c> type so this abstraction (and the whole Core project, which the
/// WASM/browser build AOT-compiles) never references the process/HTTP-using update library. The
/// desktop host maps its <c>UpdateCheckResult</c> onto this.
/// </summary>
public sealed record AppUpdateStatus(
    bool IsManaged,
    bool IsUpdateAvailable,
    string CurrentVersionDisplay,
    string? LatestVersionDisplay,
    string? SuggestedCommand,
    string? ReleaseNotesUrl,
    bool IsDismissed);

/// <summary>
/// Host-provided update capability. Implemented by the desktop host (backed by the Updates library);
/// the browser host gets <see cref="NullAppUpdateService"/> (<see cref="IsSupported"/> = false), which
/// keeps the update UI inert there per the feature scope.
/// </summary>
public interface IAppUpdateService
{
    /// <summary>False on platforms with no package-manager update path (browser). Gates all update UI.</summary>
    bool IsSupported { get; }

    /// <summary>Current app version for display, e.g. <c>v0.40.2-alpha</c> or <c>development build</c>.</summary>
    string CurrentVersionDisplay { get; }

    /// <summary>
    /// Runs a check (respecting the daily cadence cache unless <paramref name="force"/>), or returns
    /// null when unsupported / suppressed. Never throws for the expected failure modes.
    /// </summary>
    Task<AppUpdateStatus?> CheckAsync(bool force, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the user dismissed the banner for <paramref name="versionDisplay"/> (the
    /// <see cref="AppUpdateStatus.LatestVersionDisplay"/> value), so it won't reappear for that version
    /// on future launches — but a newer version still will. Persisted; a no-op when unsupported.
    /// </summary>
    void DismissVersion(string versionDisplay);

    /// <summary>
    /// One-click self-update: spawns the detached "wait for this app to exit → run the package-manager
    /// upgrade → relaunch" helper. Returns true if the helper was spawned, in which case the caller
    /// must quit the app so the upgrade can proceed. False when unsupported, not managed, or no update.
    /// </summary>
    Task<bool> TryStartSelfUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One-shot check on startup: if a previous one-click update was started but the version didn't
    /// change (the upgrade failed / didn't take effect), returns a short notice to show the user (once);
    /// otherwise null. Consumes the pending-update marker so it never fires twice.
    /// </summary>
    string? ConsumeFailedUpdateNotice();
}

/// <summary>No-op update service for hosts without a package-manager channel (browser). Shows the version only.</summary>
public sealed class NullAppUpdateService : IAppUpdateService
{
    public bool IsSupported => false;

    public string CurrentVersionDisplay { get; } = ReadEntryAssemblyVersionDisplay();

    public Task<AppUpdateStatus?> CheckAsync(bool force, CancellationToken cancellationToken = default)
        => Task.FromResult<AppUpdateStatus?>(null);

    public void DismissVersion(string versionDisplay)
    {
        // No update path here, so nothing to remember.
    }

    public Task<bool> TryStartSelfUpdateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public string? ConsumeFailedUpdateNotice() => null;

    private static string ReadEntryAssemblyVersionDisplay()
    {
        var raw = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(raw))
            return "unknown";
        // Strip any SourceLink "+build" metadata.
        var plus = raw.IndexOf('+');
        var version = plus >= 0 ? raw[..plus] : raw;
        return version is "1.0.0" ? "development build" : $"v{version}";
    }
}
