using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Caching;

/// <summary>
/// File-system-backed <see cref="IDownloadCache"/>. Stores each cached artifact as a
/// <c>&lt;sha256(url)&gt;.&lt;ext&gt;</c> blob in a directory, alongside an <c>index.json</c> manifest
/// holding the URL→file mapping plus integrity, LRU, and HTTP-validator metadata.
/// </summary>
/// <remarks>
/// <para>
/// Reads are integrity-checked (size + SHA-256): a missing, truncated, or corrupt blob is treated
/// as a miss and pruned so the caller re-downloads rather than loading garbage into the emulator.
/// </para>
/// <para>
/// A total-size cap with least-recently-used eviction keeps the cache from growing unbounded.
/// All manifest access is serialized through a <see cref="SemaphoreSlim"/>, so concurrent
/// downloads that hit the same cache stay consistent.
/// </para>
/// </remarks>
public sealed class FileDownloadCache : IDownloadCache
{
    private const string ManifestFileName = "index.json";

    private readonly string _directory;
    private readonly long _maxTotalBytes;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileDownloadCache(string directory, ILoggerFactory loggerFactory, long maxTotalBytes = DownloadCacheDefaults.MaxTotalBytes)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Cache directory must be specified.", nameof(directory));
        if (maxTotalBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTotalBytes), "Cache size cap must be positive.");

        _directory = Path.GetFullPath(directory);
        _maxTotalBytes = maxTotalBytes;
        _logger = loggerFactory.CreateLogger(typeof(FileDownloadCache).Name);
    }

    public async Task<byte[]?> TryGetAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var manifest = await LoadManifestAsync(cancellationToken);
            var entry = manifest.Entries.FirstOrDefault(e => e.Url == url);
            if (entry == null)
                return null;

            var contentPath = Path.Combine(_directory, entry.File);
            if (!File.Exists(contentPath))
            {
                _logger.LogWarning("Cached file missing for {Url}; pruning entry.", url);
                manifest.Entries.Remove(entry);
                await SaveManifestAsync(manifest, cancellationToken);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(contentPath, cancellationToken);
            if (bytes.Length != entry.Size || ComputeSha256Hex(bytes) != entry.Sha256)
            {
                _logger.LogWarning("Cached file for {Url} failed integrity check; pruning and re-downloading.", url);
                manifest.Entries.Remove(entry);
                TryDeleteFile(contentPath);
                await SaveManifestAsync(manifest, cancellationToken);
                return null;
            }

            entry.LastAccessUtc = DateTimeOffset.UtcNow;
            await SaveManifestAsync(manifest, cancellationToken);

            _logger.LogDebug("Download cache hit for {Url} ({Size} bytes).", url, entry.Size);
            return bytes;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task PutAsync(
        string url,
        byte[] content,
        string extension,
        string? displayName = null,
        string? etag = null,
        string? lastModified = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL must be specified.", nameof(url));
        ArgumentNullException.ThrowIfNull(content);

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_directory);

            var manifest = await LoadManifestAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var safeExtension = NormalizeExtension(extension);
            var fileName = $"{ComputeSha256Hex(Encoding.UTF8.GetBytes(url))}.{safeExtension}";
            var contentPath = Path.Combine(_directory, fileName);

            await File.WriteAllBytesAsync(contentPath, content, cancellationToken);

            var existing = manifest.Entries.FirstOrDefault(e => e.Url == url);
            var isNewEntry = existing == null;
            if (existing != null)
            {
                // Overwriting: drop any stale blob if the extension (hence file name) changed.
                if (!string.Equals(existing.File, fileName, StringComparison.Ordinal))
                    TryDeleteFile(Path.Combine(_directory, existing.File));
                manifest.Entries.Remove(existing);
            }

            manifest.Entries.Add(new DownloadCacheEntry
            {
                Url = url,
                File = fileName,
                Extension = safeExtension,
                Size = content.Length,
                Sha256 = ComputeSha256Hex(content),
                ETag = etag,
                LastModified = lastModified,
                DisplayName = displayName,
                SavedUtc = existing?.SavedUtc ?? now,
                LastAccessUtc = now,
            });

            EvictIfOverCap(manifest);
            await SaveManifestAsync(manifest, cancellationToken);

            _logger.LogDebug(
                "{Action} download cache entry for {Url} as {File} ({Size} bytes).",
                isNewEntry ? "Added" : "Updated",
                url,
                fileName,
                content.Length);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<DownloadCacheEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var manifest = await LoadManifestAsync(cancellationToken);
            return manifest.Entries
                .OrderByDescending(e => e.LastAccessUtc)
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task RemoveAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var manifest = await LoadManifestAsync(cancellationToken);
            var entry = manifest.Entries.FirstOrDefault(e => e.Url == url);
            if (entry == null)
                return;

            manifest.Entries.Remove(entry);
            TryDeleteFile(Path.Combine(_directory, entry.File));
            await SaveManifestAsync(manifest, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var manifest = await LoadManifestAsync(cancellationToken);
            foreach (var entry in manifest.Entries)
                TryDeleteFile(Path.Combine(_directory, entry.File));

            manifest.Entries.Clear();
            await SaveManifestAsync(manifest, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private void EvictIfOverCap(DownloadCacheManifest manifest)
    {
        foreach (var entry in DownloadCacheEviction.SelectLruEvictions(manifest.Entries, _maxTotalBytes))
        {
            manifest.Entries.Remove(entry);
            TryDeleteFile(Path.Combine(_directory, entry.File));
            _logger.LogInformation("Evicted cached {Url} ({Size} bytes) to stay under cache size cap.", entry.Url, entry.Size);
        }
    }

    private async Task<DownloadCacheManifest> LoadManifestAsync(CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(_directory, ManifestFileName);
        if (!File.Exists(manifestPath))
            return new DownloadCacheManifest();

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return new DownloadCacheManifest();

            return JsonSerializer.Deserialize(json, DownloadCacheManifestJsonContext.Default.DownloadCacheManifest)
                ?? new DownloadCacheManifest();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "Download cache manifest unreadable; starting from an empty manifest.");
            return new DownloadCacheManifest();
        }
    }

    private async Task SaveManifestAsync(DownloadCacheManifest manifest, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var manifestPath = Path.Combine(_directory, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, DownloadCacheManifestJsonContext.Default.DownloadCacheManifest);

        var tempFile = Path.Combine(_directory, $".{ManifestFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(tempFile, json, cancellationToken);
            File.Move(tempFile, manifestPath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(tempFile);
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "bin";

        var trimmed = extension.Trim().TrimStart('.').ToLowerInvariant();
        // Keep only characters that are safe in a file name; fall back to "bin" if nothing remains.
        var cleaned = new string(trimmed.Where(c => char.IsLetterOrDigit(c)).ToArray());
        return cleaned.Length == 0 ? "bin" : cleaned;
    }

    private static string ComputeSha256Hex(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to delete cache file {Path}.", path);
        }
    }
}
