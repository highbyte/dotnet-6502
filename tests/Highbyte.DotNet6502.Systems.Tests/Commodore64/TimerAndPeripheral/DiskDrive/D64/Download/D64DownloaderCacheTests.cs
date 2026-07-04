using System.Net;
using Highbyte.DotNet6502.Systems.Caching;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;

/// <summary>
/// Verifies the read-through cache wiring in <see cref="D64Downloader"/>: a first download populates
/// the cache; a second call for the same URL is served from the cache without another network fetch.
/// </summary>
public class D64DownloaderCacheTests : IDisposable
{
    private readonly string _cacheDir;

    public D64DownloaderCacheTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), $"dotnet6502-d64cache-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public async Task DownloadAndProcessDiskImage_CachesFirstDownload_AndServesSecondFromCache()
    {
        var d64Bytes = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var handler = new CountingHandler(d64Bytes);
        var httpClient = new HttpClient(handler);
        var cache = new FileDownloadCache(_cacheDir, NullLoggerFactory.Instance);

        var downloader = new D64Downloader(NullLoggerFactory.Instance, httpClient, corsProxyUrl: null, downloadCache: cache);
        var diskInfo = new C64DownloadProgramInfo(
            displayName: "Game",
            downloadUrl: "https://example.com/game.d64",
            downloadType: C64DownloadProgramType.D64);

        var first = await downloader.DownloadAndProcessDiskImage(diskInfo);
        var second = await downloader.DownloadAndProcessDiskImage(diskInfo);

        Assert.Equal(d64Bytes, first);
        Assert.Equal(d64Bytes, second);
        // The second call must be a cache hit — only one network request total.
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task DownloadAndProcessDiskImage_WithoutCache_AlwaysDownloads()
    {
        var d64Bytes = new byte[] { 1, 2 };
        var handler = new CountingHandler(d64Bytes);
        var httpClient = new HttpClient(handler);

        var downloader = new D64Downloader(NullLoggerFactory.Instance, httpClient, corsProxyUrl: null, downloadCache: null);
        var diskInfo = new C64DownloadProgramInfo(
            displayName: "Game",
            downloadUrl: "https://example.com/game.d64",
            downloadType: C64DownloadProgramType.D64);

        await downloader.DownloadAndProcessDiskImage(diskInfo);
        await downloader.DownloadAndProcessDiskImage(diskInfo);

        Assert.Equal(2, handler.RequestCount);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly byte[] _content;
        public int RequestCount { get; private set; }

        public CountingHandler(byte[] content) => _content = content;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_content),
            });
        }
    }
}
