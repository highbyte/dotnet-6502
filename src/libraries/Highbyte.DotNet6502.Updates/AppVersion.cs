using System.Reflection;

namespace Highbyte.DotNet6502.Updates;

/// <summary>
/// Reads the running app's release version, stamped into the assembly at build time by CI
/// (the release workflow passes <c>-p:Version=${TAG#v}</c> to each app's <c>publish.sh</c>).
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// The default assembly version when no <c>-p:Version</c> was passed. A locally built
    /// (non-CI) app reports this, and the update checker treats it as "unknown → skip the update
    /// check" so dev builds never nag.
    /// </summary>
    public static readonly SemanticVersion Unstamped = new(1, 0, 0);

    /// <summary>
    /// Reads the release version from the given (or entry) assembly's
    /// <see cref="AssemblyInformationalVersionAttribute"/>, stripping any <c>+build</c> metadata
    /// that SourceLink appends (e.g. <c>0.40.2-alpha+abc123</c> → <c>0.40.2-alpha</c>).
    /// Returns null when the value is missing, unparseable, or the unstamped <c>1.0.0</c> default —
    /// callers treat null as "unknown, skip the check".
    /// </summary>
    public static SemanticVersion? GetCurrent(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var raw = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return Parse(raw);
    }

    /// <summary>
    /// Parses a raw informational-version string into a release version, or null when it is
    /// missing, unparseable, or the unstamped <c>1.0.0</c> default. Exposed for testing.
    /// </summary>
    public static SemanticVersion? Parse(string? informationalVersion)
    {
        if (!SemanticVersion.TryParse(informationalVersion, out var version) || version is null)
            return null;

        // 1.0.0 (with or without +build metadata) means the app was not stamped by CI.
        if (version.Equals(Unstamped))
            return null;

        // Drop the SourceLink +build metadata so the version displays cleanly (e.g. in the About dialog).
        return version.BuildMetadata is null
            ? version
            : new SemanticVersion(version.Major, version.Minor, version.Patch, version.PrereleaseIdentifiers);
    }
}
