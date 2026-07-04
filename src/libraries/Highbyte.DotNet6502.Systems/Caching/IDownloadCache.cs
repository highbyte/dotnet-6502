namespace Highbyte.DotNet6502.Systems.Caching;

/// <summary>
/// Host-agnostic read-through cache for auto-downloaded, effectively-immutable binary content
/// keyed by its source URL (e.g. a C64 <c>.d64</c> disk image or a <c>.prg</c> program).
/// </summary>
/// <remarks>
/// The cache is keyed on the <b>original source URL</b>, never a transport-specific variant such as
/// a CORS-proxied URL. Callers store the <b>final processed artifact</b> (the extracted <c>.d64</c>
/// / the <c>.prg</c> bytes that actually get loaded), not the raw download.
///
/// Two implementations exist today:
/// <list type="bullet">
///   <item><see cref="FileDownloadCache"/> — desktop, file-blob + JSON manifest.</item>
///   <item><see cref="DelegateDownloadCache"/> — host-pluggable (e.g. a future browser IndexedDB backend).</item>
/// </list>
/// A <c>null</c> cache reference means "no caching" — callers fall back to always-download.
/// </remarks>
public interface IDownloadCache
{
    /// <summary>
    /// Returns the cached content for <paramref name="url"/> if a valid, integrity-checked entry
    /// exists; otherwise <c>null</c>. A corrupt or truncated entry is treated as a miss (and pruned)
    /// so the caller re-downloads rather than loading garbage.
    /// </summary>
    Task<byte[]?> TryGetAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores (or overwrites) <paramref name="content"/> for <paramref name="url"/>.
    /// </summary>
    /// <param name="url">The original source URL (the cache key).</param>
    /// <param name="content">The final processed artifact bytes to cache.</param>
    /// <param name="extension">Bare file extension for the cached artifact, e.g. <c>d64</c> or <c>prg</c>.</param>
    /// <param name="displayName">Optional friendly name, surfaced when listing cache entries.</param>
    /// <param name="etag">Optional HTTP <c>ETag</c> validator, stored for future conditional revalidation.</param>
    /// <param name="lastModified">Optional HTTP <c>Last-Modified</c> validator.</param>
    Task PutAsync(
        string url,
        byte[] content,
        string extension,
        string? displayName = null,
        string? etag = null,
        string? lastModified = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns metadata for all cached entries (for an inspect / clear UI).</summary>
    Task<IReadOnlyList<DownloadCacheEntry>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes the entry for <paramref name="url"/>. No-op if absent.</summary>
    Task RemoveAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>Removes every cached entry.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
