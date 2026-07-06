using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Updates;

/// <summary>
/// Host-agnostic update check: detect the install channel first (cheap, local, and gates
/// everything), and only if managed query GitHub for a newer release, comparing by semver and
/// caching the result. Returns a fully-formed <see cref="UpdateCheckResult"/> including the exact
/// <c>brew</c>/<c>scoop</c> command to run — never throws for the expected failure modes
/// (offline, rate-limited, not-managed, unstamped).
///
/// When an update is available it also logs one <see cref="LogLevel.Information"/> line via the
/// supplied <see cref="ILogger"/>. This is the single shared log point so every host emits the same
/// message; only where those logs are routed (in-app Logs pane, console, ...) differs per host.
/// </summary>
public sealed class UpdateChecker
{
    private readonly AppUpdateDescriptor _descriptor;
    private readonly InstallChannelDetector _channelDetector;
    private readonly IReleaseSource _releaseSource;
    private readonly UpdateCheckCache _cache;
    private readonly Func<SemanticVersion?> _currentVersionProvider;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly ILogger _logger;

    /// <summary>Convenience factory wiring the production GitHub release source over the given <see cref="HttpClient"/>.</summary>
    public static UpdateChecker CreateDefault(AppUpdateDescriptor descriptor, HttpClient httpClient, ILogger? logger = null)
        => new(descriptor, new GitHubReleaseClient(httpClient), logger: logger);

    public UpdateChecker(
        AppUpdateDescriptor descriptor,
        IReleaseSource releaseSource,
        InstallChannelDetector? channelDetector = null,
        UpdateCheckCache? cache = null,
        Func<SemanticVersion?>? currentVersionProvider = null,
        Func<DateTimeOffset>? utcNow = null,
        ILogger? logger = null)
    {
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _releaseSource = releaseSource ?? throw new ArgumentNullException(nameof(releaseSource));
        _channelDetector = channelDetector ?? new InstallChannelDetector();
        _cache = cache ?? new UpdateCheckCache();
        _currentVersionProvider = currentVersionProvider ?? (() => AppVersion.GetCurrent());
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<UpdateCheckResult> CheckAsync(UpdateCheckContext? context = null, CancellationToken cancellationToken = default)
    {
        context ??= new UpdateCheckContext();

        // 1. Channel first — if not managed, do nothing (not even the version read or network call).
        var channelInfo = _channelDetector.Detect(_descriptor);
        if (!channelInfo.IsManaged)
            return UpdateCheckResult.NotManaged(_currentVersionProvider());

        // 2. Current version — a local/dev build reports 1.0.0 (null here) → skip.
        var current = _currentVersionProvider();
        if (current is null)
            return new UpdateCheckResult { Status = UpdateCheckStatus.VersionUnknown, Channel = channelInfo.Channel, PackageName = channelInfo.PackageName };

        var cached = _cache.Load();

        // 3. Within the cadence window (and not forced): decide from the cached latest, no network.
        if (!context.ForceCheck && cached is { LatestTag: not null } &&
            _utcNow() - cached.LastCheckUtc < context.MinCheckInterval)
        {
            if (SemanticVersion.TryParse(cached.LatestTag, out var cachedLatest) && cachedLatest is not null)
                return BuildResult(current, cachedLatest, cached.LatestReleaseUrl, channelInfo);
        }

        // 4. Query GitHub (conditional on the cached ETag).
        try
        {
            var query = await _releaseSource.GetReleasesAsync(cached?.ETag, cancellationToken).ConfigureAwait(false);

            SemanticVersion? latest;
            string? latestUrl;
            string? latestTag;
            string? etag;

            if (query.NotModified && cached?.LatestTag != null)
            {
                SemanticVersion.TryParse(cached.LatestTag, out latest);
                latestUrl = cached.LatestReleaseUrl;
                latestTag = cached.LatestTag;
                etag = cached.ETag;
            }
            else
            {
                var newest = SelectNewestRelease(query.Releases, context.IncludePrereleases);
                latest = newest.Version;
                latestUrl = newest.Release?.HtmlUrl;
                latestTag = newest.Release?.TagName;
                etag = query.ETag;
            }

            _cache.Save(new UpdateCheckCacheData
            {
                LastCheckUtc = _utcNow(),
                ETag = etag,
                LatestTag = latestTag,
                LatestReleaseUrl = latestUrl,
            });

            if (latest is null)
                return new UpdateCheckResult { Status = UpdateCheckStatus.UpToDate, CurrentVersion = current, Channel = channelInfo.Channel, PackageName = channelInfo.PackageName };

            return BuildResult(current, latest, latestUrl, channelInfo);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "Update check failed (offline, rate-limited, or unexpected response): {Message}", ex.Message);

            // Update the timestamp so a persistent failure doesn't hammer GitHub every launch,
            // but keep the previous ETag/latest so a later success is still conditional.
            if (cached is not null)
                _cache.Save(cached with { LastCheckUtc = _utcNow() });

            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.Error,
                CurrentVersion = current,
                Channel = channelInfo.Channel,
                PackageName = channelInfo.PackageName,
                Error = ex.Message,
            };
        }
    }

    private UpdateCheckResult BuildResult(SemanticVersion current, SemanticVersion latest, string? releaseUrl, InstallChannelInfo channelInfo)
    {
        var updateAvailable = latest > current;
        if (updateAvailable)
        {
            _logger.LogInformation(
                "A newer version v{LatestVersion} is available (you have v{CurrentVersion}). Run '{UpgradeCommand}' to update.",
                latest, current, BuildUpgradeCommand(channelInfo.Channel));
        }
        return new UpdateCheckResult
        {
            Status = updateAvailable ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate,
            CurrentVersion = current,
            LatestVersion = latest,
            Channel = channelInfo.Channel,
            PackageName = channelInfo.PackageName,
            SuggestedCommand = updateAvailable ? BuildUpgradeCommand(channelInfo.Channel) : null,
            ReleaseNotesUrl = updateAvailable ? releaseUrl : null,
            ManagerExecutablePath = channelInfo.ManagerExecutablePath,
        };
    }

    /// <summary>Builds the exact upgrade command for the detected channel (Phase A: shown/copied, not auto-run).</summary>
    public string BuildUpgradeCommand(InstallChannel channel) => channel switch
    {
        InstallChannel.Homebrew when _descriptor.HomebrewIsCask => $"brew update && brew upgrade --cask {_descriptor.HomebrewPackage}",
        InstallChannel.Homebrew => $"brew update && brew upgrade {_descriptor.HomebrewPackage}",
        // ';' (not '&&') separates the metadata refresh from the app upgrade: Scoop runs under
        // PowerShell, where ';' is the portable statement separator ('&&' is PowerShell 7+ only).
        InstallChannel.Scoop => $"scoop update; scoop update {_descriptor.ScoopPackage}",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "No upgrade command for a non-managed channel."),
    };

    private static (SemanticVersion? Version, GitHubRelease? Release) SelectNewestRelease(
        IReadOnlyList<GitHubRelease> releases,
        bool includePrereleases)
    {
        SemanticVersion? bestVersion = null;
        GitHubRelease? bestRelease = null;

        foreach (var release in releases)
        {
            if (release.Draft)
                continue;
            if (release.Prerelease && !includePrereleases)
                continue;
            if (!SemanticVersion.TryParse(release.TagName, out var version) || version is null)
                continue;

            if (bestVersion is null || version > bestVersion)
            {
                bestVersion = version;
                bestRelease = release;
            }
        }

        return (bestVersion, bestRelease);
    }
}
