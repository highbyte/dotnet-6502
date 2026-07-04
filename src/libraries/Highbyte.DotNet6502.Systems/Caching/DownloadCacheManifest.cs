using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Systems.Caching;

/// <summary>
/// One entry in the download-cache manifest: a URL mapped to its cached artifact file plus the
/// metadata needed for integrity checks, LRU eviction, conditional revalidation, and UI inspection.
/// </summary>
public sealed class DownloadCacheEntry
{
    /// <summary>The original source URL this entry was downloaded from (the cache key).</summary>
    public string Url { get; set; } = "";

    /// <summary>Content file name (relative to the cache directory), e.g. <c>&lt;sha256(url)&gt;.d64</c>.</summary>
    public string File { get; set; } = "";

    /// <summary>Bare file extension of the cached artifact, e.g. <c>d64</c> / <c>prg</c>.</summary>
    public string Extension { get; set; } = "";

    /// <summary>Size of the cached content in bytes (integrity check).</summary>
    public long Size { get; set; }

    /// <summary>Lower-case hex SHA-256 of the cached content (integrity check).</summary>
    public string Sha256 { get; set; } = "";

    /// <summary>HTTP <c>ETag</c> validator from the download response, if any.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ETag { get; set; }

    /// <summary>HTTP <c>Last-Modified</c> validator from the download response, if any.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastModified { get; set; }

    /// <summary>Friendly display name for inspection UIs, if provided at store time.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; set; }

    /// <summary>When the entry was first written.</summary>
    public DateTimeOffset SavedUtc { get; set; }

    /// <summary>When the entry was last read or written (drives LRU eviction).</summary>
    public DateTimeOffset LastAccessUtc { get; set; }
}

/// <summary>
/// The <c>index.json</c> manifest stored alongside the cached content files.
/// </summary>
public sealed class DownloadCacheManifest
{
    /// <summary>On-disk manifest format version this code reads/writes.</summary>
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public List<DownloadCacheEntry> Entries { get; set; } = new();
}

/// <summary>
/// Source-generated JSON metadata for the download-cache manifest. Using the source generator
/// (rather than reflection-based serialization) keeps the manifest round-trippable in trimmed / AOT
/// hosts, matching the pattern used by the snapshot manifest.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DownloadCacheManifest))]
internal partial class DownloadCacheManifestJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
