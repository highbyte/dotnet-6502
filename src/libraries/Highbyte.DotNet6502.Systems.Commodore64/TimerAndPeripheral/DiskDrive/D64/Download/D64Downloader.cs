using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Systems.Caching;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;
using Highbyte.DotNet6502.Utils;


namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;

public class D64Downloader
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _corsProxyUrl;
    private readonly IDownloadCache? _downloadCache;

    public D64Downloader(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        string? corsProxyUrl = null,
        IDownloadCache? downloadCache = null)
    {
        _logger = loggerFactory.CreateLogger(typeof(D64Downloader).Name);
        _httpClient = httpClient;
        _corsProxyUrl = corsProxyUrl;
        _downloadCache = downloadCache;
    }


    /// <summary>
    /// Downloads and processes a disk image file (supports both .d64 and .zip files).
    /// Cache-first: a valid cached copy (keyed on the original source URL) is returned without
    /// hitting the network; otherwise the freshly downloaded / extracted .d64 is cached for reuse.
    /// </summary>
    /// <param name="diskInfo">Information about the program to download</param>
    /// <returns>The .d64 file content as byte array</returns>
    public async Task<byte[]> DownloadAndProcessDiskImage(C64DownloadProgramInfo diskInfo)
    {
        if (_downloadCache != null)
        {
            var cached = await _downloadCache.TryGetAsync(diskInfo.DownloadUrl);
            if (cached != null)
            {
                _logger.LogInformation(
                    "Using cached disk image {DisplayName} for source URL {SourceUrl} ({ByteCount} bytes)",
                    diskInfo.DisplayName,
                    diskInfo.DownloadUrl,
                    cached.Length);
                return cached;
            }
        }

        _logger.LogInformation(
            "Downloading disk image {DisplayName} from source URL {SourceUrl}",
            diskInfo.DisplayName,
            diskInfo.DownloadUrl);

        // Use CORS proxy to bypass browser CORS restrictions
        var downloadUrl = _corsProxyUrl != null ?
            _corsProxyUrl + Uri.EscapeDataString(diskInfo.DownloadUrl)
            : diskInfo.DownloadUrl;

        _logger.LogInformation(
            "Using request URL {RequestUrl} for disk image {DisplayName}",
            downloadUrl,
            diskInfo.DisplayName);

        try
        {
            byte[] d64Bytes;
            string? etag;
            string? lastModified;

            // Check the download type to determine download strategy
            if (diskInfo.DownloadType == C64DownloadProgramType.D64Zip)
            {
                _logger.LogInformation("Processing ZIP file to extract .d64");
                (d64Bytes, etag, lastModified) = await DownloadAndExtractZipD64(downloadUrl);
            }
            else
            {
                // Download direct .d64 file
                using var response = await _httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                d64Bytes = await response.Content.ReadAsByteArrayAsync();
                (etag, lastModified) = GetValidators(response);
                _logger.LogInformation("Downloaded .d64 file: {ByteCount} bytes", d64Bytes.Length);
            }

            await CacheD64Async(diskInfo, d64Bytes, etag, lastModified);
            return d64Bytes;
        }
        catch (Exception ex)
        {
            var userMessage = DownloadErrorHelper.BuildDownloadFailureMessage(
                $"disk image '{diskInfo.DisplayName}'",
                diskInfo.DownloadUrl,
                downloadUrl,
                ex);

            _logger.LogError(
                ex,
                "Failed to download disk image {DisplayName}. Source URL: {SourceUrl}. Request URL: {RequestUrl}. Details: {ErrorSummary}",
                diskInfo.DisplayName,
                diskInfo.DownloadUrl,
                downloadUrl,
                DownloadErrorHelper.FlattenExceptionMessages(ex));

            throw new InvalidOperationException(userMessage, ex);
        }
    }

    /// <summary>
    /// Downloads a ZIP file and extracts the first .d64 file in it.
    /// </summary>
    /// <param name="url">The URL to download the ZIP from</param>
    /// <returns>The extracted .d64 bytes plus the ZIP response's HTTP validators.</returns>
    private async Task<(byte[] d64Bytes, string? etag, string? lastModified)> DownloadAndExtractZipD64(string url)
    {
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var (etag, lastModified) = GetValidators(response);
        using var responseStream = await response.Content.ReadAsStreamAsync();
        var d64Bytes = D64ZipExtractor.ExtractFirstD64FromZip(responseStream, _logger);
        return (d64Bytes, etag, lastModified);
    }

    private async Task CacheD64Async(C64DownloadProgramInfo diskInfo, byte[] d64Bytes, string? etag, string? lastModified)
    {
        if (_downloadCache == null)
            return;

        try
        {
            await _downloadCache.PutAsync(diskInfo.DownloadUrl, d64Bytes, "d64", diskInfo.DisplayName, etag, lastModified);
            _logger.LogInformation(
                "Added disk image {DisplayName} to download cache from source URL {SourceUrl} ({ByteCount} bytes)",
                diskInfo.DisplayName,
                diskInfo.DownloadUrl,
                d64Bytes.Length);
        }
        catch (Exception ex)
        {
            // Caching is best-effort; a cache write failure must not fail the load.
            _logger.LogWarning(ex, "Failed to cache disk image {DisplayName} for source URL {SourceUrl}.", diskInfo.DisplayName, diskInfo.DownloadUrl);
        }
    }

    private static (string? etag, string? lastModified) GetValidators(HttpResponseMessage response)
    {
        var etag = response.Headers.ETag?.ToString();
        var lastModified = response.Content.Headers.LastModified?.ToString("o");
        return (etag, lastModified);
    }
}
