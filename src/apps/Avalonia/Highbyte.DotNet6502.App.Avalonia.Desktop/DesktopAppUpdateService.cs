using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Updates;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop;

/// <summary>
/// Desktop <see cref="IAppUpdateService"/> backed by <see cref="UpdateChecker"/>. Lives in the desktop
/// host (not Core) so the process/HTTP-using Updates library never enters the browser/WASM AOT graph.
/// The shared <see cref="UpdateChecker"/> logs an "update available" line via the host's
/// <see cref="ILoggerFactory"/> (routed to the in-app Logs tab, and console when --console-log).
/// </summary>
public sealed class DesktopAppUpdateService : IAppUpdateService
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly UpdateChecker _checker;
    private readonly EmulatorConfig _emulatorConfig;

    public DesktopAppUpdateService(ILoggerFactory loggerFactory, EmulatorConfig emulatorConfig)
    {
        _emulatorConfig = emulatorConfig;
        _checker = UpdateChecker.CreateDefault(CreateDescriptor(), _httpClient, loggerFactory.CreateLogger("UpdateCheck"));
        var current = AppVersion.GetCurrent();
        CurrentVersionDisplay = current is null ? "development build" : $"v{current}";
    }

    /// <summary>The Avalonia desktop app: package <c>dotnet-6502</c>, a Homebrew cask on macOS but a formula on Linux.</summary>
    public static AppUpdateDescriptor CreateDescriptor() => new()
    {
        HomebrewPackage = "dotnet-6502",
        HomebrewIsCask = OperatingSystem.IsMacOS(),
        ScoopPackage = "dotnet-6502",
    };

    public bool IsSupported => true;

    public string CurrentVersionDisplay { get; }

    public async Task<AppUpdateStatus?> CheckAsync(bool force, CancellationToken cancellationToken = default)
    {
        // The automatic (non-forced) startup check honors the settings toggle and the standard
        // CI / DOTNET6502_NO_UPDATE_CHECK suppressors. The explicit "Check now" button forces and ignores them.
        if (!force && (!_emulatorConfig.UpdateCheckEnabled || ConsoleUpdateCli.IsSuppressedByEnvironment()))
            return null;

        var result = await _checker.CheckAsync(new UpdateCheckContext { ForceCheck = force }, cancellationToken)
            .ConfigureAwait(false);

        var latestDisplay = result.LatestVersion is null ? null : $"v{result.LatestVersion}";
        var dismissed = ReadDismissedVersion();

        return new AppUpdateStatus(
            IsManaged: result.Channel is InstallChannel.Homebrew or InstallChannel.Scoop,
            IsUpdateAvailable: result.IsUpdateAvailable,
            CurrentVersionDisplay: result.CurrentVersion is null ? CurrentVersionDisplay : $"v{result.CurrentVersion}",
            LatestVersionDisplay: latestDisplay,
            SuggestedCommand: result.SuggestedCommand,
            ReleaseNotesUrl: result.ReleaseNotesUrl,
            IsDismissed: latestDisplay is not null && string.Equals(dismissed, latestDisplay, StringComparison.Ordinal));
    }

    public void DismissVersion(string versionDisplay)
    {
        if (string.IsNullOrWhiteSpace(versionDisplay))
            return;
        try
        {
            var path = DismissedVersionFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, versionDisplay);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: failing to remember a dismissal just means the banner may reappear.
        }
    }

    private static string? ReadDismissedVersion()
    {
        try
        {
            var path = DismissedVersionFilePath();
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Kept in its own file (not the update-check cache) so UpdateChecker's cache writes can't clobber it.
    private static string DismissedVersionFilePath()
        => Path.Combine(AppStoragePaths.GetCacheRoot(), "updates", "dismissed-version.txt");
}
