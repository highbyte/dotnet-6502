using Highbyte.DotNet6502.Systems.Caching;
using Highbyte.DotNet6502.Systems.Utils;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Host-agnostic ROM downloader, shared by every host's ROM configuration UI.
///
/// Mirrors the disk-image download path (<c>D64Downloader</c>): cache-first on the original
/// source URL, CORS proxy for browser hosts, transparent ZIP extraction, and user-facing error
/// messages via <see cref="DownloadErrorHelper"/>. Before this existed each host rolled its own
/// bare <c>HttpClient.GetByteArrayAsync</c>, which meant no caching, no archive support and
/// inconsistent error reporting.
/// </summary>
public class RomDownloader
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _corsProxyUrl;
    private readonly IDownloadCache? _downloadCache;

    public RomDownloader(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        string? corsProxyUrl = null,
        IDownloadCache? downloadCache = null)
    {
        _logger = loggerFactory.CreateLogger(typeof(RomDownloader).Name);
        _httpClient = httpClient;
        _corsProxyUrl = corsProxyUrl;
        _downloadCache = downloadCache;
    }

    /// <summary>
    /// Downloads one ROM and returns its raw bytes, extracting it from a ZIP archive when the
    /// source says so. A cached copy is returned without hitting the network.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The download or extraction failed; the message is fit to show a user.
    /// </exception>
    public async Task<byte[]> DownloadRomAsync(
        string romName,
        RomDownloadSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var cacheKey = source.ResolveCacheKey();

        if (_downloadCache != null)
        {
            var cached = await _downloadCache.TryGetAsync(cacheKey, cancellationToken);
            if (cached != null)
            {
                _logger.LogInformation(
                    "Using cached ROM {RomName} for source {CacheKey} ({ByteCount} bytes)",
                    romName, cacheKey, cached.Length);
                return cached;
            }
        }

        var requestUrl = CorsProxyHelper.ApplyCorsProxyIfNeeded(source.Url, _corsProxyUrl);

        _logger.LogInformation(
            "Downloading ROM {RomName} from {SourceUrl} (request URL {RequestUrl})",
            romName, source.Url, requestUrl);

        try
        {
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var (etag, lastModified) = GetValidators(response);

            byte[] romBytes;
            if (source.IsZipArchive)
            {
                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                romBytes = ZipImageExtractor.ExtractImageFromZip(
                    responseStream,
                    Path.GetExtension(source.ZipEntryName!),
                    ZipImageMultipleMatchBehavior.Throw,
                    _logger,
                    source.ZipEntryName);
            }
            else
            {
                romBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                _logger.LogInformation("Downloaded ROM {RomName}: {ByteCount} bytes", romName, romBytes.Length);
            }

            await CacheRomAsync(romName, cacheKey, source, romBytes, etag, lastModified, cancellationToken);
            return romBytes;
        }
        catch (Exception ex)
        {
            var userMessage = DownloadErrorHelper.BuildDownloadFailureMessage(
                $"ROM '{romName}'",
                source.Url,
                requestUrl,
                ex);

            _logger.LogError(
                ex,
                "Failed to download ROM {RomName}. Source URL: {SourceUrl}. Request URL: {RequestUrl}. Details: {ErrorSummary}",
                romName, source.Url, requestUrl, DownloadErrorHelper.FlattenExceptionMessages(ex));

            throw new InvalidOperationException(userMessage, ex);
        }
    }

    /// <summary>
    /// Downloads every ROM in <paramref name="sources"/> and returns the raw bytes per ROM name.
    /// Downloads run sequentially so a failure reports the ROM that caused it.
    /// </summary>
    public async Task<Dictionary<string, byte[]>> DownloadRomsAsync(
        IReadOnlyDictionary<string, RomDownloadSource> sources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var result = new Dictionary<string, byte[]>();
        foreach (var (romName, source) in sources)
            result[romName] = await DownloadRomAsync(romName, source, cancellationToken);
        return result;
    }

    /// <summary>
    /// Downloads every ROM into <paramref name="romDirectory"/> and returns the file name written
    /// per ROM name. A partially written file is deleted if the write fails.
    /// </summary>
    public async Task<Dictionary<string, string>> DownloadRomsToFilesAsync(
        IReadOnlyDictionary<string, RomDownloadSource> sources,
        string romDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var expandedDirectory = PathHelper.ExpandOSEnvironmentVariables(romDirectory);
        if (!Directory.Exists(expandedDirectory))
            Directory.CreateDirectory(expandedDirectory);

        var writtenFiles = new Dictionary<string, string>();
        foreach (var (romName, source) in sources)
        {
            var romBytes = await DownloadRomAsync(romName, source, cancellationToken);

            // The file name comes from the source, not the URL: a ZIP-sourced ROM must be saved
            // under its entry name, not the archive's.
            var fileName = source.ResolveFileName();
            var destination = Path.Combine(expandedDirectory, fileName);
            try
            {
                await File.WriteAllBytesAsync(destination, romBytes, cancellationToken);
            }
            catch (Exception ex)
            {
                if (File.Exists(destination))
                    File.Delete(destination);
                throw new InvalidOperationException($"Error saving ROM '{romName}' to {destination}: {ex.Message}", ex);
            }

            writtenFiles[romName] = fileName;
        }
        return writtenFiles;
    }

    private async Task CacheRomAsync(
        string romName,
        string cacheKey,
        RomDownloadSource source,
        byte[] romBytes,
        string? etag,
        string? lastModified,
        CancellationToken cancellationToken)
    {
        if (_downloadCache == null)
            return;

        try
        {
            await _downloadCache.PutAsync(
                cacheKey, romBytes, source.ResolveExtension(), romName, etag, lastModified, cancellationToken);
        }
        catch (Exception ex)
        {
            // Caching is best-effort; a cache write failure must not fail the download.
            _logger.LogWarning(ex, "Failed to cache ROM {RomName} for source {CacheKey}.", romName, cacheKey);
        }
    }

    private static (string? etag, string? lastModified) GetValidators(HttpResponseMessage response)
    {
        var etag = response.Headers.ETag?.ToString();
        var lastModified = response.Content.Headers.LastModified?.ToString("o");
        return (etag, lastModified);
    }
}
