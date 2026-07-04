namespace Highbyte.DotNet6502.Updates;

/// <summary>
/// Detects whether this instance was installed via Homebrew or Scoop (vs. a portable zip of the
/// same binary), which gates the entire update feature. Biases hard to
/// <see cref="InstallChannel.NotManaged"/> on any ambiguity: a false "managed" would show a
/// <c>brew</c>/<c>scoop</c> command that does nothing, whereas a false "not-managed" merely does
/// nothing — the safe failure.
///
/// Three stages: (1) an authoritative <c>install-channel</c> marker file dropped by the manifest,
/// (2) path-heuristic corroboration on the run directory, and (3) a runtime sanity check that the
/// manager is actually resolvable and reports this package installed. Only stage 3 can *confirm*
/// managed; anything short of it collapses to not-managed.
/// </summary>
public sealed class InstallChannelDetector
{
    /// <summary>Single-line sidecar naming the channel (<c>homebrew</c> / <c>scoop</c>), optionally <c>homebrew:&lt;pkg&gt;</c>.</summary>
    public const string MarkerFileName = "install-channel";

    private readonly IInstallChannelProbe _probe;
    private readonly TimeSpan _commandTimeout;

    public InstallChannelDetector(IInstallChannelProbe? probe = null, TimeSpan? commandTimeout = null)
    {
        _probe = probe ?? SystemInstallChannelProbe.Instance;
        _commandTimeout = commandTimeout ?? TimeSpan.FromSeconds(5);
    }

    public InstallChannelInfo Detect(AppUpdateDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var candidate = ReadMarkerChannel();
        if (candidate is null)
            return InstallChannelInfo.NotManaged; // absent marker ⇒ not-managed (portable)

        var channel = candidate.Value;
        var packageName = descriptor.PackageNameFor(channel);

        return channel switch
        {
            InstallChannel.Homebrew => ConfirmHomebrew(packageName, descriptor.HomebrewIsCask),
            InstallChannel.Scoop => ConfirmScoop(packageName),
            _ => InstallChannelInfo.NotManaged,
        };
    }

    /// <summary>Reads the channel from the marker, checking the next-to-binary location then the macOS cask support dir.</summary>
    private InstallChannel? ReadMarkerChannel()
    {
        foreach (var path in MarkerFilePaths())
        {
            var line = _probe.ReadFileFirstLine(path);
            if (line is null)
                continue;

            // Format: "homebrew" | "scoop" | "homebrew:<pkg>" | "scoop:<pkg>". Only the channel is used here;
            // per-app package names come from the AppUpdateDescriptor.
            var channelToken = line.Split(':', 2)[0].Trim().ToLowerInvariant();
            switch (channelToken)
            {
                case "homebrew":
                case "brew":
                    return InstallChannel.Homebrew;
                case "scoop":
                    return InstallChannel.Scoop;
            }
        }

        return null;
    }

    private IEnumerable<string> MarkerFilePaths()
    {
        // 1. Next to the binary (Homebrew formula + Scoop write it into the install dir).
        yield return Path.Combine(_probe.BaseDirectory, MarkerFileName);

        // 2. macOS cask: the .app runs from /Applications and can't carry a marker cleanly, so the
        //    cask writes it to a support dir instead (see CaskSupportDirMarkerPath).
        var caskMarker = CaskSupportDirMarkerPath();
        if (caskMarker != null)
            yield return caskMarker;
    }

    /// <summary>
    /// macOS support-dir marker path written by the Homebrew cask <c>postflight</c>:
    /// <c>~/Library/Application Support/Highbyte/DotNet6502/install-channel</c>. Null on other OSes.
    /// Kept in sync with the cask manifest.
    /// </summary>
    public string? CaskSupportDirMarkerPath()
    {
        if (_probe.OS != OSPlatformKind.MacOS)
            return null;
        var home = _probe.HomeDirectory;
        if (string.IsNullOrWhiteSpace(home))
            return null;
        return Path.Combine(home, "Library", "Application Support", "Highbyte", "DotNet6502", MarkerFileName);
    }

    private InstallChannelInfo ConfirmHomebrew(string packageName, bool isCask)
    {
        var brew = _probe.ResolveExecutable("brew", HomebrewBinDirectories());
        if (brew is null)
            return InstallChannelInfo.NotManaged;

        // `brew list --versions <pkg>` exits 0 and prints the package+version only when installed.
        var args = isCask
            ? new[] { "list", "--cask", "--versions", packageName }
            : new[] { "list", "--versions", packageName };
        var result = _probe.RunCommand(brew, args, _commandTimeout);
        if (result is null || result.ExitCode != 0 || !result.StandardOutput.Contains(packageName, StringComparison.OrdinalIgnoreCase))
            return InstallChannelInfo.NotManaged;

        return new InstallChannelInfo(InstallChannel.Homebrew, packageName, brew);
    }

    private InstallChannelInfo ConfirmScoop(string packageName)
    {
        var scoop = _probe.ResolveExecutable("scoop", ScoopShimDirectories());
        if (scoop is null)
            return InstallChannelInfo.NotManaged;

        // `scoop list <pkg>` lists the app row only when installed; the app name appears in stdout.
        var result = _probe.RunCommand(scoop, new[] { "list", packageName }, _commandTimeout);
        if (result is null || result.ExitCode != 0 || !result.StandardOutput.Contains(packageName, StringComparison.OrdinalIgnoreCase))
            return InstallChannelInfo.NotManaged;

        return new InstallChannelInfo(InstallChannel.Scoop, packageName, scoop);
    }

    private IEnumerable<string> HomebrewBinDirectories()
    {
        var prefix = _probe.GetEnvironmentVariable("HOMEBREW_PREFIX");
        if (!string.IsNullOrWhiteSpace(prefix))
            yield return Path.Combine(prefix, "bin");

        yield return "/opt/homebrew/bin";        // Apple Silicon
        yield return "/usr/local/bin";           // Intel macOS

        var home = _probe.HomeDirectory;
        if (!string.IsNullOrWhiteSpace(home))
            yield return Path.Combine(home, ".linuxbrew", "bin");
        yield return "/home/linuxbrew/.linuxbrew/bin"; // Linuxbrew default
    }

    private IEnumerable<string> ScoopShimDirectories()
    {
        var scoopHome = _probe.GetEnvironmentVariable("SCOOP");
        if (!string.IsNullOrWhiteSpace(scoopHome))
            yield return Path.Combine(scoopHome, "shims");

        var home = _probe.HomeDirectory;
        if (!string.IsNullOrWhiteSpace(home))
            yield return Path.Combine(home, "scoop", "shims");
    }
}
