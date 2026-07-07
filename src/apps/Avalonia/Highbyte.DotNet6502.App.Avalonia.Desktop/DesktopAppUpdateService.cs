using System;
using System.Collections.Generic;
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
    private readonly ILogger _logger;

    public DesktopAppUpdateService(ILoggerFactory loggerFactory, EmulatorConfig emulatorConfig)
    {
        _emulatorConfig = emulatorConfig;
        _logger = loggerFactory.CreateLogger("UpdateCheck");
        _checker = UpdateChecker.CreateDefault(CreateDescriptor(), _httpClient, _logger);
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

    public async Task<bool> TryStartSelfUpdateAsync(CancellationToken cancellationToken = default)
    {
        // Confirm managed + update-available right now, and get the resolved manager path.
        var result = await _checker.CheckAsync(new UpdateCheckContext { ForceCheck = true }, cancellationToken).ConfigureAwait(false);
        if (!result.IsUpdateAvailable || result.ManagerExecutablePath is null)
        {
            _logger.LogWarning("Self-update requested, but no update is available or the package manager could not be resolved.");
            return false;
        }

        var descriptor = CreateDescriptor();
        var spawned = UpdateApplier.TrySpawnDetachedRelaunch(
            result.ManagerExecutablePath,
            descriptor.UpgradeCommandArgs(result.Channel),
            Environment.ProcessId,
            BuildRelaunchSpec(result.Channel, result.ManagerExecutablePath, descriptor.HomebrewPackage));

        if (spawned)
        {
            // Remember what we were on, so the next launch can tell whether the upgrade took effect.
            WritePendingUpdate(CurrentVersionDisplay);
            _logger.LogInformation(
                "Starting self-update ({Command}). The app will quit, upgrade, and relaunch; upgrade output goes to {LogPath}.",
                result.SuggestedCommand, UpdateApplier.GetUpdateLogPath());
        }
        else
        {
            _logger.LogWarning("Could not start the update helper process; the app will stay open.");
        }

        return spawned;
    }

    public string? ConsumeFailedUpdateNotice()
    {
        string fromVersion;
        var path = PendingUpdateFilePath();
        try
        {
            if (!File.Exists(path))
                return null;
            fromVersion = File.ReadAllText(path).Trim();
            File.Delete(path); // consume: only ever notify once per attempt
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        // Version changed since the attempt → the upgrade took effect → nothing to report.
        if (string.IsNullOrEmpty(fromVersion) || !string.Equals(fromVersion, CurrentVersionDisplay, StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(fromVersion))
                _logger.LogInformation("Self-update completed: now on {Version}.", CurrentVersionDisplay);
            return null;
        }

        // Still on the same version → the upgrade failed or didn't take effect.
        _logger.LogWarning("A previous one-click update did not complete; still on {Version}. See {LogPath}.",
            fromVersion, UpdateApplier.GetUpdateLogPath());
        return $"The last update didn't complete — you're still on {fromVersion}. See the log at {UpdateApplier.GetUpdateLogPath()}";
    }

    private static string PendingUpdateFilePath()
        => Path.Combine(AppStoragePaths.GetCacheRoot(), "updates", "pending-update.txt");

    private static void WritePendingUpdate(string fromVersionDisplay)
    {
        try
        {
            var path = PendingUpdateFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, fromVersionDisplay);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: not remembering just means no post-update notice.
        }
    }

    // How to relaunch the Avalonia GUI after the upgrade. If the app was started as
    // `dotnet <app>.dll` (the local dev/update-trigger flow), reconstruct that exact launch on every
    // OS. Otherwise macOS uses `open -a "<AppName>"` (robust across the cask replacing the bundle),
    // Linux Homebrew relaunches via the stable bin symlink, and Windows relaunches the current
    // executable path (Scoop's `current` junction stays valid after the update).
    private static RelaunchSpec BuildRelaunchSpec(
        InstallChannel channel,
        string managerExecutablePath,
        string homebrewLauncherName)
        => BuildRelaunchSpec(
            channel,
            managerExecutablePath,
            homebrewLauncherName,
            Environment.ProcessPath ?? string.Empty,
            Environment.GetCommandLineArgs(),
            GetCurrentOS());

    internal static RelaunchSpec BuildRelaunchSpec(
        InstallChannel channel,
        string? managerExecutablePath,
        string homebrewLauncherName,
        string processPath,
        string[] commandLineArgs,
        OSPlatformKind os)
    {
        if (IsDotNetHost(processPath) && commandLineArgs.Length > 0)
        {
            var relaunchArgs = new List<string>(commandLineArgs.Length);
            relaunchArgs.AddRange(commandLineArgs);
            return new RelaunchSpec(processPath, relaunchArgs);
        }

        if (os == OSPlatformKind.MacOS)
            return new RelaunchSpec("/usr/bin/open", new[] { "-a", "DotNet 6502 Emulator" });

        var appArgs = commandLineArgs.Length > 1
            ? commandLineArgs[1..]
            : Array.Empty<string>();

        if (os == OSPlatformKind.Linux && channel == InstallChannel.Homebrew)
        {
            var managerDirectory = string.IsNullOrWhiteSpace(managerExecutablePath)
                ? null
                : Path.GetDirectoryName(managerExecutablePath);
            var launcher = string.IsNullOrWhiteSpace(managerDirectory)
                ? homebrewLauncherName
                : Path.Combine(managerDirectory, homebrewLauncherName);
            return new RelaunchSpec(launcher, appArgs);
        }

        return new RelaunchSpec(processPath, appArgs);
    }

    private static OSPlatformKind GetCurrentOS()
    {
        if (OperatingSystem.IsWindows())
            return OSPlatformKind.Windows;
        if (OperatingSystem.IsMacOS())
            return OSPlatformKind.MacOS;
        if (OperatingSystem.IsLinux())
            return OSPlatformKind.Linux;
        return OSPlatformKind.Other;
    }

    private static bool IsDotNetHost(string processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
            return false;

        var fileName = Path.GetFileName(processPath);
        return string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "dotnet.exe", StringComparison.OrdinalIgnoreCase);
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
