namespace Highbyte.DotNet6502.Updates;

/// <summary>
/// Detects whether this instance was installed via Homebrew or Scoop (vs. a portable zip of the
/// same binary), which gates the entire update feature. Biases hard to
/// <see cref="InstallChannel.NotManaged"/> on any ambiguity: a false "managed" would show a
/// <c>brew</c>/<c>scoop</c> command that does nothing, whereas a false "not-managed" merely does
/// nothing — the safe failure.
///
/// Three stages: (1) an authoritative <c>install-channel</c> marker file dropped by the manifest,
/// (2) a conservative path heuristic on the run directory, used only as a fallback when the marker is
/// absent (Scoop's <c>apps/&lt;pkg&gt;/current</c> layout, or a Homebrew formula's <c>Cellar</c> path),
/// and (3) a runtime sanity check that the manager is actually resolvable and reports this package
/// installed. Only stage 3 can *confirm* managed; anything short of it collapses to not-managed.
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

        // Authoritative marker first; if it's absent, fall back to a conservative path heuristic on the
        // run directory. Either way the candidate is only a *guess* — stage 3 (ConfirmHomebrew /
        // ConfirmScoop) still has to see the package manager report this package installed.
        var candidate = ReadMarkerChannel() ?? InferChannelFromPath(descriptor);
        if (candidate is null)
            return InstallChannelInfo.NotManaged; // no marker and no matching install path ⇒ not-managed (portable)

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

    /// <summary>
    /// Fallback used only when no marker is present: guess a channel from the directory the app runs
    /// from. Conservative and biased to not-managed — it recognises just the two unambiguous layouts,
    /// and the guess is still confirmed by the package manager before anything is shown:
    /// <list type="bullet">
    ///   <item>Scoop installs the app under <c>&lt;scoop&gt;/apps/&lt;package&gt;/current</c>.</item>
    ///   <item>A Homebrew <em>formula</em> installs under <c>&lt;prefix&gt;/Cellar/&lt;formula&gt;/&lt;version&gt;/…</c>.</item>
    /// </list>
    /// The Homebrew <em>cask</em> is deliberately excluded: the <c>.app</c> normally runs from
    /// <c>/Applications</c> (not the Cellar), so it stays marker-driven.
    /// </summary>
    private InstallChannel? InferChannelFromPath(AppUpdateDescriptor descriptor)
    {
        var baseDir = _probe.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDir))
            return null;

        var normalized = baseDir.Replace('\\', '/');

        if (LooksLikeScoopInstallPath(normalized, descriptor.ScoopPackage))
            return InstallChannel.Scoop;

        if (!descriptor.HomebrewIsCask && LooksLikeHomebrewCellarPath(normalized, descriptor.HomebrewPackage))
            return InstallChannel.Homebrew;

        return null;
    }

    /// <summary>True when the run directory sits under a Scoop <c>apps/&lt;package&gt;/current</c> tree.</summary>
    private bool LooksLikeScoopInstallPath(string normalizedBaseDir, string package)
    {
        var haystack = normalizedBaseDir.ToLowerInvariant();
        var suffix = $"/apps/{package}/current".ToLowerInvariant();

        // Default install roots are literally named "scoop"; also honour a custom $SCOOP root.
        if (haystack.Contains($"/scoop{suffix}", StringComparison.Ordinal))
            return true;

        foreach (var root in ScoopRoots())
        {
            var rootPrefix = root.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
            if (haystack.Contains($"{rootPrefix}{suffix}", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>True when the run directory is under a known Homebrew prefix's <c>Cellar/&lt;formula&gt;/…</c> tree.</summary>
    private bool LooksLikeHomebrewCellarPath(string normalizedBaseDir, string formula)
    {
        foreach (var prefix in HomebrewPrefixes())
        {
            var cellarRoot = prefix.Replace('\\', '/').TrimEnd('/') + $"/Cellar/{formula}/";
            if (normalizedBaseDir.StartsWith(cellarRoot, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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
        => HomebrewPrefixes().Select(prefix => Path.Combine(prefix, "bin"));

    /// <summary>Known Homebrew install prefixes (env override first, then the platform defaults).</summary>
    private IEnumerable<string> HomebrewPrefixes()
    {
        var prefix = _probe.GetEnvironmentVariable("HOMEBREW_PREFIX");
        if (!string.IsNullOrWhiteSpace(prefix))
            yield return prefix;

        yield return "/opt/homebrew";            // Apple Silicon
        yield return "/usr/local";               // Intel macOS

        var home = _probe.HomeDirectory;
        if (!string.IsNullOrWhiteSpace(home))
            yield return Path.Combine(home, ".linuxbrew");
        yield return "/home/linuxbrew/.linuxbrew"; // Linuxbrew default
    }

    private IEnumerable<string> ScoopShimDirectories()
        => ScoopRoots().Select(root => Path.Combine(root, "shims"));

    /// <summary>Known Scoop install roots (env override first, then <c>~/scoop</c>).</summary>
    private IEnumerable<string> ScoopRoots()
    {
        var scoopHome = _probe.GetEnvironmentVariable("SCOOP");
        if (!string.IsNullOrWhiteSpace(scoopHome))
            yield return scoopHome;

        var home = _probe.HomeDirectory;
        if (!string.IsNullOrWhiteSpace(home))
            yield return Path.Combine(home, "scoop");
    }
}
