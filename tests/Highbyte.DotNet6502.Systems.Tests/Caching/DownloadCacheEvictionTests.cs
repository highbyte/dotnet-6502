using Highbyte.DotNet6502.Systems.Caching;

namespace Highbyte.DotNet6502.Systems.Tests.Caching;

public class DownloadCacheEvictionTests
{
    [Fact]
    public void SelectLruEvictions_ReturnsEmpty_WhenUnderCap()
    {
        var entries = new[]
        {
            Entry("a", size: 10, lastAccessOffset: 0),
            Entry("b", size: 10, lastAccessOffset: 1)
        };

        Assert.Empty(DownloadCacheEviction.SelectLruEvictions(entries, maxTotalBytes: 20));
    }

    [Fact]
    public void SelectLruEvictions_SelectsOldestEntriesUntilUnderCap()
    {
        var entries = new[]
        {
            Entry("oldest", size: 10, lastAccessOffset: 0),
            Entry("middle", size: 10, lastAccessOffset: 1),
            Entry("newest", size: 10, lastAccessOffset: 2)
        };

        var evictions = DownloadCacheEviction.SelectLruEvictions(entries, maxTotalBytes: 10);

        Assert.Equal(["oldest", "middle"], evictions.Select(e => e.Url));
    }

    [Fact]
    public void SelectLruEvictions_KeepsLastEntry_EvenWhenOverCap()
    {
        var entries = new[]
        {
            Entry("oversized", size: 100, lastAccessOffset: 0)
        };

        Assert.Empty(DownloadCacheEviction.SelectLruEvictions(entries, maxTotalBytes: 10));
    }

    private static DownloadCacheEntry Entry(string url, long size, int lastAccessOffset)
        => new()
        {
            Url = url,
            Size = size,
            LastAccessUtc = DateTimeOffset.UnixEpoch.AddSeconds(lastAccessOffset)
        };
}
