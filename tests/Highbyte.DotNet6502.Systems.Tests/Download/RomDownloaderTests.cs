using System.IO.Compression;
using System.Net;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Caching;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Download;

public class RomDownloaderTests
{
    private static readonly byte[] ChargenBytes = { 0x00, 0x1C, 0x22, 0x2A, 0x2E, 0x2C, 0x20, 0x1E };
    private static readonly byte[] SystemRomBytes = { 0xA9, 0x00, 0x8D, 0x00, 0x04, 0x4C, 0x00, 0xF8 };

    private static byte[] BuildZip(params (string Name, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }
        }
        return buffer.ToArray();
    }

    private static RomDownloader BuildDownloader(
        StubHttpMessageHandler handler,
        string? corsProxyUrl = null,
        IDownloadCache? cache = null)
        => new(NullLoggerFactory.Instance, new HttpClient(handler), corsProxyUrl, cache);

    [Fact]
    public async Task A_Bare_File_Is_Downloaded_As_Is()
    {
        var handler = new StubHttpMessageHandler(_ => Ok(SystemRomBytes));
        var downloader = BuildDownloader(handler);

        var bytes = await downloader.DownloadRomAsync("apple2", new RomDownloadSource("https://example.com/apple.rom"));

        Assert.Equal(SystemRomBytes, bytes);
        Assert.Equal("https://example.com/apple.rom", Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task A_Named_Entry_Is_Extracted_From_A_Zip_Archive()
    {
        var zip = BuildZip(
            ("341011d0.bin", new byte[] { 0xFF }),
            ("3410036.BIN", ChargenBytes),
            ("3410065A.BIN", new byte[] { 0xEE }));
        var handler = new StubHttpMessageHandler(_ => Ok(zip));
        var downloader = BuildDownloader(handler);

        var bytes = await downloader.DownloadRomAsync(
            "chargen",
            new RomDownloadSource("https://example.com/ROMS.ZIP", ZipEntryName: "3410036.BIN"));

        Assert.Equal(ChargenBytes, bytes);
    }

    [Fact]
    public async Task An_Ambiguous_Archive_Still_Resolves_Because_The_Entry_Is_Named()
    {
        // The real archive holds dozens of .bin files; extension matching alone could not pick one.
        var entries = Enumerable.Range(0, 20)
            .Select(i => ($"other{i}.BIN", new byte[] { (byte)i }))
            .Append(("3410036.BIN", ChargenBytes))
            .ToArray();
        var handler = new StubHttpMessageHandler(_ => Ok(BuildZip(entries)));
        var downloader = BuildDownloader(handler);

        var bytes = await downloader.DownloadRomAsync(
            "chargen",
            new RomDownloadSource("https://example.com/ROMS.ZIP", ZipEntryName: "3410036.BIN"));

        Assert.Equal(ChargenBytes, bytes);
    }

    [Fact]
    public async Task A_Missing_Zip_Entry_Reports_A_Useful_Error()
    {
        var handler = new StubHttpMessageHandler(_ => Ok(BuildZip(("something-else.bin", new byte[] { 0x01 }))));
        var downloader = BuildDownloader(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadRomAsync(
            "chargen",
            new RomDownloadSource("https://example.com/ROMS.ZIP", ZipEntryName: "3410036.BIN")));

        Assert.Contains("chargen", ex.Message);
    }

    [Fact]
    public async Task An_Http_Failure_Reports_A_Useful_Error()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var downloader = BuildDownloader(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadRomAsync(
            "apple2", new RomDownloadSource("https://example.com/missing.rom")));

        Assert.Contains("apple2", ex.Message);
    }

    [Fact]
    public async Task The_Cors_Proxy_Is_Applied_To_The_Request_But_Not_The_Cache_Key()
    {
        var cache = new RecordingDownloadCache();
        var handler = new StubHttpMessageHandler(_ => Ok(SystemRomBytes));
        var downloader = BuildDownloader(handler, corsProxyUrl: "https://proxy/fetch?url=", cache: cache);

        await downloader.DownloadRomAsync("apple2", new RomDownloadSource("https://example.com/apple.rom"));

        Assert.StartsWith("https://proxy/fetch?url=", Assert.Single(handler.Requests));
        Assert.Equal("https://example.com/apple.rom", Assert.Single(cache.Puts).Url);
    }

    [Fact]
    public async Task A_Cached_Rom_Is_Returned_Without_Hitting_The_Network()
    {
        var cache = new RecordingDownloadCache();
        cache.Seed("https://example.com/apple.rom", SystemRomBytes);
        var handler = new StubHttpMessageHandler(_ => Ok(new byte[] { 0xDE, 0xAD }));
        var downloader = BuildDownloader(handler, cache: cache);

        var bytes = await downloader.DownloadRomAsync("apple2", new RomDownloadSource("https://example.com/apple.rom"));

        Assert.Equal(SystemRomBytes, bytes);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_Zip_Sourced_Rom_Is_Cached_Under_A_Key_Including_Its_Entry()
    {
        var cache = new RecordingDownloadCache();
        var handler = new StubHttpMessageHandler(_ => Ok(BuildZip(("3410036.BIN", ChargenBytes))));
        var downloader = BuildDownloader(handler, cache: cache);

        await downloader.DownloadRomAsync(
            "chargen", new RomDownloadSource("https://example.com/ROMS.ZIP", ZipEntryName: "3410036.BIN"));

        var put = Assert.Single(cache.Puts);
        Assert.Equal("https://example.com/ROMS.ZIP#3410036.BIN", put.Url);
        Assert.Equal(ChargenBytes, put.Content);   // the extracted ROM, not the archive
        Assert.Equal("bin", put.Extension);
    }

    [Fact]
    public async Task A_Cache_Write_Failure_Does_Not_Fail_The_Download()
    {
        var handler = new StubHttpMessageHandler(_ => Ok(SystemRomBytes));
        var downloader = BuildDownloader(handler, cache: new ThrowingDownloadCache());

        var bytes = await downloader.DownloadRomAsync("apple2", new RomDownloadSource("https://example.com/apple.rom"));

        Assert.Equal(SystemRomBytes, bytes);
    }

    [Fact]
    public async Task Downloading_To_Files_Saves_A_Zip_Entry_Under_Its_Own_Name()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dotnet6502-romdownloader-" + Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new StubHttpMessageHandler(request =>
                request.RequestUri!.AbsoluteUri.EndsWith(".ZIP", StringComparison.OrdinalIgnoreCase)
                    ? Ok(BuildZip(("3410036.BIN", ChargenBytes)))
                    : Ok(SystemRomBytes));
            var downloader = BuildDownloader(handler);

            var written = await downloader.DownloadRomsToFilesAsync(
                new Dictionary<string, RomDownloadSource>
                {
                    { "apple2", new RomDownloadSource("https://example.com/apple.rom") },
                    { "chargen", new RomDownloadSource("https://example.com/ROMS.ZIP", ZipEntryName: "3410036.BIN") },
                },
                directory);

            Assert.Equal("apple.rom", written["apple2"]);
            Assert.Equal("3410036.BIN", written["chargen"]);

            // Crucially the archive name is never written to disk.
            Assert.False(File.Exists(Path.Combine(directory, "ROMS.ZIP")));
            Assert.Equal(ChargenBytes, await File.ReadAllBytesAsync(Path.Combine(directory, "3410036.BIN")));
            Assert.Equal(SystemRomBytes, await File.ReadAllBytesAsync(Path.Combine(directory, "apple.rom")));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Downloading_Several_Roms_Returns_Them_Keyed_By_Name()
    {
        var handler = new StubHttpMessageHandler(request =>
            Ok(request.RequestUri!.AbsoluteUri.Contains("basic") ? new byte[] { 0x01 } : new byte[] { 0x02 }));
        var downloader = BuildDownloader(handler);

        var roms = await downloader.DownloadRomsAsync(new Dictionary<string, RomDownloadSource>
        {
            { "basic", new RomDownloadSource("https://example.com/basic.bin") },
            { "kernal", new RomDownloadSource("https://example.com/kernal.bin") },
        });

        Assert.Equal(new byte[] { 0x01 }, roms["basic"]);
        Assert.Equal(new byte[] { 0x02 }, roms["kernal"]);
    }

    private static HttpResponseMessage Ok(byte[] content)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public List<string> Requests { get; } = new();

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class RecordingDownloadCache : IDownloadCache
    {
        private readonly Dictionary<string, byte[]> _entries = new();

        public List<(string Url, byte[] Content, string Extension)> Puts { get; } = new();

        public void Seed(string url, byte[] content) => _entries[url] = content;

        public Task<byte[]?> TryGetAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.TryGetValue(url, out var content) ? content : null);

        public Task PutAsync(string url, byte[] content, string extension, string? displayName = null,
            string? etag = null, string? lastModified = null, CancellationToken cancellationToken = default)
        {
            Puts.Add((url, content, extension));
            _entries[url] = content;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DownloadCacheEntry>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DownloadCacheEntry>>(Array.Empty<DownloadCacheEntry>());

        public Task RemoveAsync(string url, CancellationToken cancellationToken = default)
        {
            _entries.Remove(url);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _entries.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDownloadCache : IDownloadCache
    {
        public Task<byte[]?> TryGetAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult<byte[]?>(null);

        public Task PutAsync(string url, byte[] content, string extension, string? displayName = null,
            string? etag = null, string? lastModified = null, CancellationToken cancellationToken = default)
            => throw new IOException("cache unavailable");

        public Task<IReadOnlyList<DownloadCacheEntry>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DownloadCacheEntry>>(Array.Empty<DownloadCacheEntry>());

        public Task RemoveAsync(string url, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
