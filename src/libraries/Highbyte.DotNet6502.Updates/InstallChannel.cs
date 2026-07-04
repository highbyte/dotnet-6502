namespace Highbyte.DotNet6502.Updates;

/// <summary>
/// How this instance of the app was installed. Only package-manager installs get an update flow;
/// everything else (portable zip, dev build, or any ambiguity) is <see cref="NotManaged"/> and does
/// nothing.
/// </summary>
public enum InstallChannel
{
    /// <summary>Not installed via a supported package manager. No update check, no nudge.</summary>
    NotManaged = 0,

    /// <summary>Installed via Homebrew (cask on macOS GUI, formula for console apps / Linux).</summary>
    Homebrew,

    /// <summary>Installed via Scoop (Windows).</summary>
    Scoop,
}

/// <summary>
/// Result of install-channel detection. <see cref="ManagerExecutablePath"/> is the full resolved
/// path to the package manager (GUI apps don't inherit the shell PATH), populated only once the
/// channel is confirmed managed.
/// </summary>
public sealed record InstallChannelInfo(
    InstallChannel Channel,
    string? PackageName = null,
    string? ManagerExecutablePath = null)
{
    public static readonly InstallChannelInfo NotManaged = new(InstallChannel.NotManaged);

    public bool IsManaged => Channel is InstallChannel.Homebrew or InstallChannel.Scoop;
}

/// <summary>
/// Per-app details the update flow needs but the shared detector can't know on its own: what this
/// app is called in each package manager, and (for Homebrew) whether it's a cask or a formula —
/// which changes both the confirmation query and the suggested <c>brew upgrade</c> command.
/// </summary>
public sealed record AppUpdateDescriptor
{
    /// <summary>Homebrew package name (cask token or formula name), e.g. <c>dotnet-6502-terminal</c>.</summary>
    public required string HomebrewPackage { get; init; }

    /// <summary>True for the macOS GUI cask; false for a formula (console apps / Linux GUI).</summary>
    public bool HomebrewIsCask { get; init; }

    /// <summary>Scoop app name, e.g. <c>dotnet-6502-terminal</c>.</summary>
    public required string ScoopPackage { get; init; }

    /// <summary>Package name for the currently detected/confirmed channel.</summary>
    public string PackageNameFor(InstallChannel channel) => channel switch
    {
        InstallChannel.Homebrew => HomebrewPackage,
        InstallChannel.Scoop => ScoopPackage,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "No package name for a non-managed channel."),
    };
}
