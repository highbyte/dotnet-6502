namespace Highbyte.DotNet6502.Updates;

public enum UpdateCheckStatus
{
    /// <summary>Not installed via a package manager (portable / dev / ambiguous). Nothing to do.</summary>
    NotManaged = 0,

    /// <summary>App assembly wasn't stamped by CI (a local/dev build); can't compare, skip.</summary>
    VersionUnknown,

    /// <summary>Managed install, already on the newest applicable release.</summary>
    UpToDate,

    /// <summary>Managed install, a newer release exists — see <see cref="UpdateCheckResult.SuggestedCommand"/>.</summary>
    UpdateAvailable,

    /// <summary>The check failed (offline, rate-limited, parse error). See <see cref="UpdateCheckResult.Error"/>.</summary>
    Error,
}

/// <summary>Outcome of an update check. <see cref="SuggestedCommand"/> is populated only when an update is available.</summary>
public sealed record UpdateCheckResult
{
    public required UpdateCheckStatus Status { get; init; }
    public SemanticVersion? CurrentVersion { get; init; }
    public SemanticVersion? LatestVersion { get; init; }
    public InstallChannel Channel { get; init; }
    public string? PackageName { get; init; }
    public string? SuggestedCommand { get; init; }
    public string? ReleaseNotesUrl { get; init; }
    public string? Error { get; init; }

    /// <summary>Full path to the resolved package manager (brew/scoop), for actually running the upgrade. Null when not managed.</summary>
    public string? ManagerExecutablePath { get; init; }

    public bool IsUpdateAvailable => Status == UpdateCheckStatus.UpdateAvailable;

    public static UpdateCheckResult NotManaged(SemanticVersion? current = null) => new()
    {
        Status = UpdateCheckStatus.NotManaged,
        CurrentVersion = current,
        Channel = InstallChannel.NotManaged,
    };
}

/// <summary>Caller-supplied knobs for a single update check.</summary>
public sealed record UpdateCheckContext
{
    /// <summary>Bypass the cadence window (and use of a cached result): a manual "Check now" / <c>--check-update</c>.</summary>
    public bool ForceCheck { get; init; }

    /// <summary>Minimum time between network checks; within it a cached result is reused. Default 24h.</summary>
    public TimeSpan MinCheckInterval { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Consider prerelease (<c>-alpha</c>) releases. True here since every release is tagged <c>-alpha</c>.</summary>
    public bool IncludePrereleases { get; init; } = true;
}
