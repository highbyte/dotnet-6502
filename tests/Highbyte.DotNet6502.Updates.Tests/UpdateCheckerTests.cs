using Highbyte.DotNet6502.Updates;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Highbyte.DotNet6502.Updates.Tests;

public class UpdateCheckerTests : IDisposable
{
    private static readonly AppUpdateDescriptor Descriptor = new()
    {
        HomebrewPackage = "dotnet-6502-terminal",
        HomebrewIsCask = false,
        ScoopPackage = "dotnet-6502-terminal",
    };

    private readonly string _cacheFile = Path.Combine(Path.GetTempPath(), $"update-check-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_cacheFile))
            File.Delete(_cacheFile);
    }

    private sealed class FakeReleaseSource : IReleaseSource
    {
        private readonly ReleaseQueryResult _result;
        private readonly Exception? _throw;
        public int CallCount { get; private set; }

        public FakeReleaseSource(ReleaseQueryResult result) => _result = result;
        public FakeReleaseSource(Exception toThrow) { _throw = toThrow; _result = null!; }

        public Task<ReleaseQueryResult> GetReleasesAsync(string? etag, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_throw != null)
                throw _throw;
            return Task.FromResult(_result);
        }
    }

    private static ReleaseQueryResult Releases(params string[] tags)
    {
        var list = tags.Select(t => new GitHubRelease { TagName = t, Prerelease = t.Contains('-'), HtmlUrl = $"https://example/releases/{t}" }).ToList();
        return new ReleaseQueryResult(NotModified: false, ETag: "\"etag1\"", Releases: list);
    }

    private InstallChannelDetector ManagedHomebrewDetector()
    {
        var probe = new FakeInstallChannelProbe { OS = OSPlatformKind.Linux };
        probe.Files["/app/install-channel"] = "homebrew";
        probe.ResolvableExecutables["brew"] = "/opt/homebrew/bin/brew";
        probe.CommandResults["/opt/homebrew/bin/brew list --versions dotnet-6502-terminal"] =
            new ProcessRunResult(0, "dotnet-6502-terminal 0.40.1\n");
        return new InstallChannelDetector(probe);
    }

    private InstallChannelDetector NotManagedDetector()
        => new(new FakeInstallChannelProbe { OS = OSPlatformKind.Linux }); // no marker

    private UpdateChecker MakeChecker(IReleaseSource source, InstallChannelDetector detector, string currentVersion, ILogger? logger = null)
        => new(
            Descriptor,
            source,
            detector,
            new UpdateCheckCache(_cacheFile),
            currentVersionProvider: () => AppVersion.Parse(currentVersion),
            utcNow: () => DateTimeOffset.UnixEpoch + TimeSpan.FromDays(1000),
            logger: logger);

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullDisposable.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }

    [Fact]
    public async Task LogsInformation_WhenUpdateAvailable()
    {
        var logger = new CapturingLogger();
        var checker = MakeChecker(new FakeReleaseSource(Releases("v0.40.1-alpha", "v0.40.2-alpha")), ManagedHomebrewDetector(), "0.40.1-alpha", logger);

        await checker.CheckAsync();

        Assert.Contains(logger.Messages, m => m.Contains("v0.40.2-alpha") && m.Contains("brew upgrade dotnet-6502-terminal"));
    }

    [Fact]
    public async Task DoesNotLog_WhenUpToDate()
    {
        var logger = new CapturingLogger();
        var checker = MakeChecker(new FakeReleaseSource(Releases("v0.40.2-alpha")), ManagedHomebrewDetector(), "0.40.2-alpha", logger);

        await checker.CheckAsync();

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public async Task UpdateAvailable_BuildsCommandAndReleaseUrl()
    {
        var checker = MakeChecker(new FakeReleaseSource(Releases("v0.40.1-alpha", "v0.40.2-alpha")), ManagedHomebrewDetector(), "0.40.1-alpha");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("0.40.2-alpha", result.LatestVersion!.ToString());
        Assert.Equal("brew upgrade dotnet-6502-terminal", result.SuggestedCommand);
        Assert.Equal("https://example/releases/v0.40.2-alpha", result.ReleaseNotesUrl);
    }

    [Fact]
    public async Task UpToDate_WhenOnNewestRelease()
    {
        var checker = MakeChecker(new FakeReleaseSource(Releases("v0.40.1-alpha", "v0.40.2-alpha")), ManagedHomebrewDetector(), "0.40.2-alpha");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Null(result.SuggestedCommand);
    }

    [Fact]
    public async Task NotManaged_ShortCircuits_NoNetwork()
    {
        var source = new FakeReleaseSource(Releases("v0.40.2-alpha"));
        var checker = MakeChecker(source, NotManagedDetector(), "0.40.1-alpha");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateCheckStatus.NotManaged, result.Status);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public async Task VersionUnknown_WhenAppNotStamped()
    {
        var source = new FakeReleaseSource(Releases("v0.40.2-alpha"));
        var checker = MakeChecker(source, ManagedHomebrewDetector(), "1.0.0"); // unstamped

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateCheckStatus.VersionUnknown, result.Status);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public async Task CadenceWindow_ReusesCache_NoSecondNetworkCall()
    {
        var source = new FakeReleaseSource(Releases("v0.40.1-alpha", "v0.40.2-alpha"));
        var detector = ManagedHomebrewDetector();

        var first = await MakeChecker(source, detector, "0.40.1-alpha").CheckAsync();
        Assert.Equal(UpdateCheckStatus.UpdateAvailable, first.Status);
        Assert.Equal(1, source.CallCount);

        // Second check within the 24h window reuses the cache written by the first.
        var second = await MakeChecker(source, detector, "0.40.1-alpha").CheckAsync();
        Assert.Equal(UpdateCheckStatus.UpdateAvailable, second.Status);
        Assert.Equal("brew upgrade dotnet-6502-terminal", second.SuggestedCommand);
        Assert.Equal(1, source.CallCount); // no new network call
    }

    [Fact]
    public async Task ForceCheck_BypassesCadenceCache()
    {
        var source = new FakeReleaseSource(Releases("v0.40.2-alpha"));
        var detector = ManagedHomebrewDetector();

        await MakeChecker(source, detector, "0.40.1-alpha").CheckAsync();
        await MakeChecker(source, detector, "0.40.1-alpha").CheckAsync(new UpdateCheckContext { ForceCheck = true });

        Assert.Equal(2, source.CallCount);
    }

    [Fact]
    public async Task NetworkFailure_ReturnsErrorStatus()
    {
        var checker = MakeChecker(new FakeReleaseSource(new HttpRequestException("offline")), ManagedHomebrewDetector(), "0.40.1-alpha");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateCheckStatus.Error, result.Status);
        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task IgnoresDraftsAndPrereleasesWhenExcluded()
    {
        var releases = new ReleaseQueryResult(false, "\"e\"", new[]
        {
            new GitHubRelease { TagName = "v0.41.0-alpha", Prerelease = true, HtmlUrl = "u1" },
            new GitHubRelease { TagName = "v0.40.2", Prerelease = false, HtmlUrl = "u2" },
        });
        var checker = MakeChecker(new FakeReleaseSource(releases), ManagedHomebrewDetector(), "0.40.1");

        var result = await checker.CheckAsync(new UpdateCheckContext { IncludePrereleases = false });

        Assert.Equal("0.40.2", result.LatestVersion!.ToString()); // prerelease 0.41.0-alpha excluded
    }
}
