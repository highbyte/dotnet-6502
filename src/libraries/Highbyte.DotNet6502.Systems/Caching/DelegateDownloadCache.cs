namespace Highbyte.DotNet6502.Systems.Caching;

/// <summary>
/// Delegate-backed <see cref="IDownloadCache"/>. Lets a host supply its own storage backend without
/// the caching subsystem depending on host-specific APIs — mirroring the
/// <c>DelegateScriptStore</c> pattern used for the Lua key/value store.
/// </summary>
/// <remarks>
/// Intended for the Avalonia Browser (WASM) host, where the natural backend is IndexedDB accessed via
/// JS interop (an inherently asynchronous, filesystem-free store). That backend is not wired yet;
/// this type exists so it can be slotted in without touching the C64 download call sites.
/// </remarks>
public sealed class DelegateDownloadCache : IDownloadCache
{
    private readonly Func<string, CancellationToken, Task<byte[]?>> _tryGet;
    private readonly Func<DownloadCacheEntry, byte[], CancellationToken, Task> _put;
    private readonly Func<CancellationToken, Task<IReadOnlyList<DownloadCacheEntry>>> _list;
    private readonly Func<string, CancellationToken, Task> _remove;
    private readonly Func<CancellationToken, Task> _clear;

    public DelegateDownloadCache(
        Func<string, CancellationToken, Task<byte[]?>> tryGet,
        Func<DownloadCacheEntry, byte[], CancellationToken, Task> put,
        Func<CancellationToken, Task<IReadOnlyList<DownloadCacheEntry>>> list,
        Func<string, CancellationToken, Task> remove,
        Func<CancellationToken, Task> clear)
    {
        _tryGet = tryGet ?? throw new ArgumentNullException(nameof(tryGet));
        _put = put ?? throw new ArgumentNullException(nameof(put));
        _list = list ?? throw new ArgumentNullException(nameof(list));
        _remove = remove ?? throw new ArgumentNullException(nameof(remove));
        _clear = clear ?? throw new ArgumentNullException(nameof(clear));
    }

    public Task<byte[]?> TryGetAsync(string url, CancellationToken cancellationToken = default)
        => _tryGet(url, cancellationToken);

    public Task PutAsync(
        string url,
        byte[] content,
        string extension,
        string? displayName = null,
        string? etag = null,
        string? lastModified = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new DownloadCacheEntry
        {
            Url = url,
            File = url,
            Extension = extension,
            Size = content.Length,
            ETag = etag,
            LastModified = lastModified,
            DisplayName = displayName,
            SavedUtc = now,
            LastAccessUtc = now,
        };
        return _put(entry, content, cancellationToken);
    }

    public Task<IReadOnlyList<DownloadCacheEntry>> ListAsync(CancellationToken cancellationToken = default)
        => _list(cancellationToken);

    public Task RemoveAsync(string url, CancellationToken cancellationToken = default)
        => _remove(url, cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken = default)
        => _clear(cancellationToken);
}
