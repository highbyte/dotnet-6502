namespace Highbyte.DotNet6502.Updates;

/// <summary>Fetches the repository's releases. Abstracted so the update checker can be tested with a fake.</summary>
public interface IReleaseSource
{
    /// <summary>
    /// Fetches releases, sending <paramref name="etag"/> as a conditional request when non-null.
    /// Returns <see cref="ReleaseQueryResult.NotModified"/> when the server answers 304.
    /// </summary>
    Task<ReleaseQueryResult> GetReleasesAsync(string? etag, CancellationToken cancellationToken = default);
}
