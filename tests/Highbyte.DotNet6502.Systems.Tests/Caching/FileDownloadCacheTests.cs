using System.Text;
using Highbyte.DotNet6502.Systems.Caching;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Caching;

public class FileDownloadCacheTests : IDisposable
{
    private readonly string _directory;

    public FileDownloadCacheTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"dotnet6502-cache-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private FileDownloadCache CreateCache(long maxTotalBytes = DownloadCacheDefaults.MaxTotalBytes)
        => new(_directory, NullLoggerFactory.Instance, maxTotalBytes);

    [Fact]
    public async Task TryGet_ReturnsNull_OnMiss()
    {
        var cache = CreateCache();
        Assert.Null(await cache.TryGetAsync("https://example.com/game.d64"));
    }

    [Fact]
    public async Task Put_ThenTryGet_RoundTripsContent()
    {
        var cache = CreateCache();
        var url = "https://example.com/game.d64";
        var content = Encoding.UTF8.GetBytes("disk-image-bytes");

        await cache.PutAsync(url, content, "d64", displayName: "My Game");
        var roundTripped = await cache.TryGetAsync(url);

        Assert.NotNull(roundTripped);
        Assert.Equal(content, roundTripped);
    }

    [Fact]
    public async Task Put_WritesContentFileWithExtension_AndManifest()
    {
        var cache = CreateCache();
        await cache.PutAsync("https://example.com/game.prg", new byte[] { 1, 2, 3 }, ".PRG");

        Assert.True(File.Exists(Path.Combine(_directory, "index.json")));
        Assert.Single(Directory.EnumerateFiles(_directory, "*.prg"));
    }

    [Fact]
    public async Task Put_IsKeyedOnUrl_NotProxiedVariant()
    {
        var cache = CreateCache();
        var url = "https://example.com/game.d64";
        await cache.PutAsync(url, new byte[] { 9 }, "d64");

        // The proxied URL is a different key → miss; the original URL → hit.
        Assert.Null(await cache.TryGetAsync("https://proxy/?url=" + url));
        Assert.NotNull(await cache.TryGetAsync(url));
    }

    [Fact]
    public async Task Put_OverwritesExistingEntry()
    {
        var cache = CreateCache();
        var url = "https://example.com/game.d64";

        await cache.PutAsync(url, new byte[] { 1 }, "d64");
        await cache.PutAsync(url, new byte[] { 2, 2 }, "d64");

        var content = await cache.TryGetAsync(url);
        Assert.Equal(new byte[] { 2, 2 }, content);
        var entries = await cache.ListAsync();
        Assert.Single(entries);
    }

    [Fact]
    public async Task TryGet_TreatsCorruptContentAsMiss_AndPrunes()
    {
        var cache = CreateCache();
        var url = "https://example.com/game.d64";
        await cache.PutAsync(url, new byte[] { 1, 2, 3, 4 }, "d64");

        // Corrupt the stored blob so it no longer matches the manifest size/checksum.
        var contentFile = Directory.EnumerateFiles(_directory, "*.d64").Single();
        await File.WriteAllBytesAsync(contentFile, new byte[] { 0xFF });

        Assert.Null(await cache.TryGetAsync(url));
        // Entry is pruned so a re-download is forced next time.
        Assert.Empty(await cache.ListAsync());
    }

    [Fact]
    public async Task TryGet_MissingContentFile_IsPruned()
    {
        var cache = CreateCache();
        var url = "https://example.com/game.d64";
        await cache.PutAsync(url, new byte[] { 1, 2, 3 }, "d64");

        File.Delete(Directory.EnumerateFiles(_directory, "*.d64").Single());

        Assert.Null(await cache.TryGetAsync(url));
        Assert.Empty(await cache.ListAsync());
    }

    [Fact]
    public async Task Put_StoresValidators_ExposedViaList()
    {
        var cache = CreateCache();
        await cache.PutAsync("https://example.com/game.d64", new byte[] { 1 }, "d64",
            displayName: "Game", etag: "\"abc\"", lastModified: "Mon, 01 Jan 2024 00:00:00 GMT");

        var entry = Assert.Single(await cache.ListAsync());
        Assert.Equal("Game", entry.DisplayName);
        Assert.Equal("\"abc\"", entry.ETag);
        Assert.Equal("Mon, 01 Jan 2024 00:00:00 GMT", entry.LastModified);
        Assert.Equal(1, entry.Size);
    }

    [Fact]
    public async Task Remove_DeletesEntryAndContentFile()
    {
        var cache = CreateCache();
        var url = "https://example.com/game.d64";
        await cache.PutAsync(url, new byte[] { 1, 2 }, "d64");

        await cache.RemoveAsync(url);

        Assert.Null(await cache.TryGetAsync(url));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.d64"));
    }

    [Fact]
    public async Task Clear_RemovesAllEntriesAndFiles()
    {
        var cache = CreateCache();
        await cache.PutAsync("https://example.com/a.d64", new byte[] { 1 }, "d64");
        await cache.PutAsync("https://example.com/b.prg", new byte[] { 2 }, "prg");

        await cache.ClearAsync();

        Assert.Empty(await cache.ListAsync());
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.d64"));
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.prg"));
    }

    [Fact]
    public async Task Put_EvictsLeastRecentlyUsed_WhenOverSizeCap()
    {
        // Cap of 20 bytes; each entry is 10 bytes, so the 3rd Put must evict one.
        var cache = CreateCache(maxTotalBytes: 20);
        var ten = new byte[10];

        await cache.PutAsync("https://example.com/a.d64", ten, "d64");
        await cache.PutAsync("https://example.com/b.d64", ten, "d64");

        // Touch "a" so "b" becomes the least-recently-used.
        Assert.NotNull(await cache.TryGetAsync("https://example.com/a.d64"));

        await cache.PutAsync("https://example.com/c.d64", ten, "d64");

        Assert.NotNull(await cache.TryGetAsync("https://example.com/a.d64"));
        Assert.NotNull(await cache.TryGetAsync("https://example.com/c.d64"));
        Assert.Null(await cache.TryGetAsync("https://example.com/b.d64"));
    }

    [Fact]
    public async Task Put_SingleArtifactLargerThanCap_IsStillUsable()
    {
        var cache = CreateCache(maxTotalBytes: 4);
        var url = "https://example.com/big.d64";
        var big = new byte[16];

        await cache.PutAsync(url, big, "d64");

        Assert.NotNull(await cache.TryGetAsync(url));
    }

    [Fact]
    public async Task NewCacheInstance_ReadsPersistedManifest()
    {
        var url = "https://example.com/game.d64";
        var content = new byte[] { 4, 5, 6 };
        await CreateCache().PutAsync(url, content, "d64");

        // A fresh instance over the same directory should see the persisted entry.
        var reopened = await CreateCache().TryGetAsync(url);
        Assert.Equal(content, reopened);
    }
}
