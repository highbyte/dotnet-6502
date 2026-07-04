using System.Net;
using System.Text.Json;

namespace Highbyte.DotNet6502.Updates;

/// <summary>
/// Queries the GitHub REST API for a repository's releases, with conditional-request (ETag) support
/// to stay well under the 60 req/hr unauthenticated rate limit.
/// </summary>
/// <remarks>
/// Uses <c>GET /releases</c> (not <c>/releases/latest</c>, which excludes prereleases — every release
/// here is tagged <c>-alpha</c>). Sends a <c>User-Agent</c> as GitHub requires.
/// </remarks>
public sealed class GitHubReleaseClient : IReleaseSource
{
    public const string DefaultOwner = "highbyte";
    public const string DefaultRepo = "dotnet-6502";
    private const string UserAgent = "Highbyte.DotNet6502-UpdateChecker";

    private readonly HttpClient _httpClient;
    private readonly string _releasesUrl;

    public GitHubReleaseClient(
        HttpClient httpClient,
        string owner = DefaultOwner,
        string repo = DefaultRepo,
        int perPage = 30)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _releasesUrl = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page={perPage}";
    }

    public async Task<ReleaseQueryResult> GetReleasesAsync(string? etag, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _releasesUrl);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        // GitHub returns weak ETags (e.g. W/"abc"); ParseAdd handles the W/ prefix that the
        // EntityTagHeaderValue(string) constructor rejects. TryParseAdd keeps a malformed cached
        // value from throwing — worst case we just do an unconditional request.
        if (!string.IsNullOrEmpty(etag))
            request.Headers.IfNoneMatch.TryParseAdd(etag);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified)
            return new ReleaseQueryResult(NotModified: true, etag, Array.Empty<GitHubRelease>());

        response.EnsureSuccessStatusCode();

        var newEtag = response.Headers.ETag?.ToString() ?? etag;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var releases = await JsonSerializer.DeserializeAsync(
            stream,
            UpdatesJsonContext.Default.GitHubReleaseArray,
            cancellationToken).ConfigureAwait(false);

        return new ReleaseQueryResult(NotModified: false, newEtag, releases ?? Array.Empty<GitHubRelease>());
    }
}
