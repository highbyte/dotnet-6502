using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;
using Highbyte.DotNet6502.Utils;


namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;

public class D64Downloader
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _corsProxyUrl;

    public D64Downloader(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        string? corsProxyUrl = null)
    {
        _logger = loggerFactory.CreateLogger(typeof(D64Downloader).Name);
        _httpClient = httpClient;
        _corsProxyUrl = corsProxyUrl;
    }


    /// <summary>
    /// Downloads and processes a disk image file (supports both .d64 and .zip files)
    /// </summary>
    /// <param name="diskInfo">Information about the program to download</param>
    /// <returns>The .d64 file content as byte array</returns>
    public async Task<byte[]> DownloadAndProcessDiskImage(C64DownloadProgramInfo diskInfo)
    {
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
            // Check the download type to determine download strategy
            if (diskInfo.DownloadType == C64DownloadProgramType.D64Zip)
            {
                _logger.LogInformation("Processing ZIP file to extract .d64");
                return await DownloadAndExtractZipD64(downloadUrl);
            }

            // Download direct .d64 file
            using var response = await _httpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();
            var d64Bytes = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation("Downloaded .d64 file: {ByteCount} bytes", d64Bytes.Length);
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
    /// <returns>The .d64 file content as byte array</returns>
    private async Task<byte[]> DownloadAndExtractZipD64(string url)
    {
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var responseStream = await response.Content.ReadAsStreamAsync();
        return D64ZipExtractor.ExtractFirstD64FromZip(responseStream, _logger);
    }
}
