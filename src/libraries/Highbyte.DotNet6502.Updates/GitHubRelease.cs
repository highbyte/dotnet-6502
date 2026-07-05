using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Updates;

/// <summary>A GitHub release, as returned by <c>GET /repos/{owner}/{repo}/releases</c>.</summary>
public sealed record GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    [JsonPropertyName("draft")]
    public bool Draft { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }
}

/// <summary>
/// Outcome of a release query. <see cref="NotModified"/> is true when the server answered 304 to a
/// conditional request (the caller should reuse its cached list); otherwise <see cref="Releases"/>
/// holds the fresh list and <see cref="ETag"/> the value to send next time.
/// </summary>
public sealed record ReleaseQueryResult(
    bool NotModified,
    string? ETag,
    IReadOnlyList<GitHubRelease> Releases);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
[JsonSerializable(typeof(GitHubRelease[]))]
[JsonSerializable(typeof(List<GitHubRelease>))]
[JsonSerializable(typeof(UpdateCheckCacheData))]
internal partial class UpdatesJsonContext : JsonSerializerContext
{
}
