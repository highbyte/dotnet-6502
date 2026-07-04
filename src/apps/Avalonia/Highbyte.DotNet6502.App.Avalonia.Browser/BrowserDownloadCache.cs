using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Highbyte.DotNet6502.Systems.Caching;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Browser;

[SupportedOSPlatform("browser")]
internal static partial class BrowserDownloadCache
{
    public static async Task<IDownloadCache?> TryCreateAsync(ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(BrowserDownloadCache));
        try
        {
            if (!await JSInterop.IsDownloadCacheAvailableAsync())
            {
                logger.LogWarning("IndexedDB download cache is not available; browser downloads will not be cached.");
                return null;
            }

            return new DelegateDownloadCache(
                tryGet: (url, cancellationToken) => TryGetAsync(url, logger, cancellationToken),
                put: PutAsync,
                list: ListAsync,
                remove: RemoveAsync,
                clear: ClearAsync);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize IndexedDB download cache; browser downloads will not be cached.");
            return null;
        }
    }

    private static async Task<byte[]?> TryGetAsync(string url, ILogger logger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var json = await JSInterop.TryGetDownloadCacheEntryAsync(url);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var record = JsonSerializer.Deserialize(json, HostConfigJsonContext.Default.BrowserDownloadCacheRecord);
            if (record == null || string.IsNullOrEmpty(record.Base64))
                return null;

            var content = Convert.FromBase64String(record.Base64);
            if (record.Entry.Size != content.Length || !string.Equals(record.Entry.Sha256, ComputeSha256Hex(content), StringComparison.Ordinal))
            {
                logger.LogWarning("IndexedDB download cache entry for {Url} failed integrity check; pruning entry.", url);
                await JSInterop.RemoveDownloadCacheEntryAsync(url);
                return null;
            }

            return content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "IndexedDB download cache read failed for {Url}; treating as a cache miss.", url);
            return null;
        }
    }

    private static async Task PutAsync(DownloadCacheEntry entry, byte[] content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        entry.Size = content.Length;
        entry.Sha256 = ComputeSha256Hex(content);

        var entryJson = JsonSerializer.Serialize(entry, HostConfigJsonContext.Default.DownloadCacheEntry);
        var base64 = Convert.ToBase64String(content);
        await JSInterop.PutDownloadCacheEntryAsync(entryJson, base64);
    }

    private static async Task<IReadOnlyList<DownloadCacheEntry>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var json = await JSInterop.ListDownloadCacheEntriesAsync();
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize(json, HostConfigJsonContext.Default.ListDownloadCacheEntry) ?? [];
    }

    private static async Task RemoveAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        cancellationToken.ThrowIfCancellationRequested();
        await JSInterop.RemoveDownloadCacheEntryAsync(url);
    }

    private static async Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await JSInterop.ClearDownloadCacheAsync();
    }

    private static string ComputeSha256Hex(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [SupportedOSPlatform("browser")]
    private static partial class JSInterop
    {
        [JSImport("isDownloadCacheAvailable", "BrowserDownloadCache")]
        public static partial Task<bool> IsDownloadCacheAvailableAsync();

        [JSImport("tryGetDownloadCacheEntry", "BrowserDownloadCache")]
        public static partial Task<string?> TryGetDownloadCacheEntryAsync(string url);

        [JSImport("putDownloadCacheEntry", "BrowserDownloadCache")]
        public static partial Task PutDownloadCacheEntryAsync(string entryJson, string base64);

        [JSImport("listDownloadCacheEntries", "BrowserDownloadCache")]
        public static partial Task<string?> ListDownloadCacheEntriesAsync();

        [JSImport("removeDownloadCacheEntry", "BrowserDownloadCache")]
        public static partial Task RemoveDownloadCacheEntryAsync(string url);

        [JSImport("clearDownloadCache", "BrowserDownloadCache")]
        public static partial Task ClearDownloadCacheAsync();
    }
}

internal sealed class BrowserDownloadCacheRecord
{
    public DownloadCacheEntry Entry { get; set; } = new();
    public string Base64 { get; set; } = string.Empty;
}
