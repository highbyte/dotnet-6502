namespace Highbyte.DotNet6502.Systems.Caching;

/// <summary>
/// Shared least-recently-used eviction policy for download-cache backends. Storage-specific
/// backends decide how to delete the selected entries.
/// </summary>
public static class DownloadCacheEviction
{
    public static IReadOnlyList<DownloadCacheEntry> SelectLruEvictions(
        IReadOnlyCollection<DownloadCacheEntry> entries,
        long maxTotalBytes)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (maxTotalBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTotalBytes), "Cache size cap must be positive.");

        var total = entries.Sum(e => e.Size);
        if (total <= maxTotalBytes || entries.Count <= 1)
            return [];

        var remainingCount = entries.Count;
        var evictions = new List<DownloadCacheEntry>();
        foreach (var entry in entries.OrderBy(e => e.LastAccessUtc))
        {
            if (total <= maxTotalBytes || remainingCount <= 1)
                break;

            evictions.Add(entry);
            total -= entry.Size;
            remainingCount--;
        }

        return evictions;
    }
}
