using System.Globalization;
using System.Numerics;

namespace Highbyte.DotNet6502.Updates;

/// <summary>
/// Minimal Semantic Versioning 2.0.0 value type: parse + precedence comparison.
///
/// Exists instead of taking a NuGet dependency (NuGet.Versioning / Semver) so the update
/// checker stays dependency-free and out of the published emulator packages. Only the subset the
/// update flow needs is implemented: <c>MAJOR.MINOR.PATCH</c> with an optional <c>-prerelease</c>
/// and an optional <c>+build</c> metadata part. A leading <c>v</c> (as in the git tag
/// <c>v0.40.2-alpha</c>) is tolerated on parse. Build metadata is parsed but, per the spec, ignored
/// for precedence, so <c>0.40.2-alpha</c> sorts below <c>0.40.2</c> and above <c>0.40.1-alpha</c>.
/// </summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    /// <summary>Dot-separated prerelease identifiers; empty when this is a release version.</summary>
    public IReadOnlyList<string> PrereleaseIdentifiers { get; }

    /// <summary>The raw <c>+build</c> metadata (without the <c>+</c>), or null. Ignored for precedence.</summary>
    public string? BuildMetadata { get; }

    public bool IsPrerelease => PrereleaseIdentifiers.Count > 0;

    public SemanticVersion(
        int major,
        int minor,
        int patch,
        IReadOnlyList<string>? prereleaseIdentifiers = null,
        string? buildMetadata = null)
    {
        if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
        if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
        if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch));

        Major = major;
        Minor = minor;
        Patch = patch;
        PrereleaseIdentifiers = prereleaseIdentifiers ?? Array.Empty<string>();
        BuildMetadata = buildMetadata;
    }

    public static bool TryParse(string? input, out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var s = input.Trim();
        if (s.StartsWith('v') || s.StartsWith('V'))
            s = s[1..];

        // Peel off build metadata (+...), then the prerelease (-...), leaving the numeric core.
        string? build = null;
        var plus = s.IndexOf('+');
        if (plus >= 0)
        {
            build = s[(plus + 1)..];
            s = s[..plus];
            if (build.Length == 0)
                return false;
        }

        string? prerelease = null;
        var dash = s.IndexOf('-');
        if (dash >= 0)
        {
            prerelease = s[(dash + 1)..];
            s = s[..dash];
        }

        var core = s.Split('.');
        if (core.Length != 3)
            return false;
        if (!TryParseCoreNumber(core[0], out var major) ||
            !TryParseCoreNumber(core[1], out var minor) ||
            !TryParseCoreNumber(core[2], out var patch))
            return false;

        string[] prereleaseIds = Array.Empty<string>();
        if (prerelease != null)
        {
            prereleaseIds = prerelease.Split('.');
            foreach (var id in prereleaseIds)
            {
                if (id.Length == 0)
                    return false; // empty identifier (e.g. trailing/leading/double dot) is invalid
            }
        }

        version = new SemanticVersion(major, minor, patch, prereleaseIds, build);
        return true;
    }

    private static bool TryParseCoreNumber(string s, out int value)
        => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
            return 1;

        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;

        // A release version has higher precedence than a prerelease of the same core.
        var thisPre = IsPrerelease;
        var otherPre = other.IsPrerelease;
        if (!thisPre && !otherPre) return 0;
        if (!thisPre) return 1;
        if (!otherPre) return -1;

        var len = Math.Min(PrereleaseIdentifiers.Count, other.PrereleaseIdentifiers.Count);
        for (var i = 0; i < len; i++)
        {
            var a = PrereleaseIdentifiers[i];
            var b = other.PrereleaseIdentifiers[i];
            var aNumeric = TryAsNumber(a, out var an);
            var bNumeric = TryAsNumber(b, out var bn);

            if (aNumeric && bNumeric)
            {
                c = an.CompareTo(bn);
                if (c != 0) return c;
            }
            else if (aNumeric)
            {
                return -1; // numeric identifiers have lower precedence than alphanumeric
            }
            else if (bNumeric)
            {
                return 1;
            }
            else
            {
                c = string.CompareOrdinal(a, b);
                if (c != 0) return c;
            }
        }

        // All shared identifiers equal: the version with more identifiers has higher precedence.
        return PrereleaseIdentifiers.Count.CompareTo(other.PrereleaseIdentifiers.Count);
    }

    private static bool TryAsNumber(string s, out BigInteger value)
    {
        value = default;
        foreach (var ch in s)
        {
            if (ch is < '0' or > '9')
                return false;
        }
        return BigInteger.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public bool Equals(SemanticVersion? other) => other is not null && CompareTo(other) == 0;
    public override bool Equals(object? obj) => Equals(obj as SemanticVersion);
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, IsPrerelease);

    public static bool operator <(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) < 0;
    public static bool operator >(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) > 0;
    public static bool operator <=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) <= 0;
    public static bool operator >=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) >= 0;
    public static bool operator ==(SemanticVersion? a, SemanticVersion? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(SemanticVersion? a, SemanticVersion? b) => !(a == b);

    public override string ToString()
    {
        var core = $"{Major}.{Minor}.{Patch}";
        if (IsPrerelease)
            core += "-" + string.Join('.', PrereleaseIdentifiers);
        if (BuildMetadata != null)
            core += "+" + BuildMetadata;
        return core;
    }
}
