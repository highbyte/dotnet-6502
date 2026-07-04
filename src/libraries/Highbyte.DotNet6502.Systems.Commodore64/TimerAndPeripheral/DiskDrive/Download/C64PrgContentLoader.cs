using Highbyte.DotNet6502.Systems.Caching;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;

public class C64PrgContentLoader : IC64AutoLoadContentLoader
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _corsProxyUrl;
    private readonly IDownloadCache? _downloadCache;

    public C64PrgContentLoader(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        string? corsProxyUrl = null,
        IDownloadCache? downloadCache = null)
    {
        _logger = loggerFactory.CreateLogger(typeof(C64PrgContentLoader).Name);
        _httpClient = httpClient;
        _corsProxyUrl = corsProxyUrl;
        _downloadCache = downloadCache;
    }

    public async Task LoadAsync(C64DownloadProgramInfo programInfo, C64 c64)
    {
        var downloadUrl = _corsProxyUrl != null
            ? _corsProxyUrl + Uri.EscapeDataString(programInfo.DownloadUrl)
            : programInfo.DownloadUrl;

        try
        {
            var prgBytes = await GetPrgBytesAsync(programInfo, downloadUrl);

            BinaryLoader.Load(
                c64.Mem,
                prgBytes,
                out ushort loadedAtAddress,
                out ushort fileLength);

            _logger.LogInformation(
                "Direct loaded PRG {DisplayName} at address {LoadedAtAddress:X4}, length {FileLength} bytes",
                programInfo.DisplayName,
                loadedAtAddress,
                fileLength);
        }
        catch (Exception ex)
        {
            var userMessage = DownloadErrorHelper.BuildDownloadFailureMessage(
                $"PRG program '{programInfo.DisplayName}'",
                programInfo.DownloadUrl,
                downloadUrl,
                ex);

            _logger.LogError(
                ex,
                "Failed to download PRG program {DisplayName}. Source URL: {SourceUrl}. Request URL: {RequestUrl}. Details: {ErrorSummary}",
                programInfo.DisplayName,
                programInfo.DownloadUrl,
                downloadUrl,
                DownloadErrorHelper.FlattenExceptionMessages(ex));

            throw new InvalidOperationException(userMessage, ex);
        }
    }

    /// <summary>
    /// Cache-first fetch of the PRG bytes. Returns a valid cached copy (keyed on the original source
    /// URL) without hitting the network; otherwise downloads and caches the bytes for reuse.
    /// </summary>
    private async Task<byte[]> GetPrgBytesAsync(C64DownloadProgramInfo programInfo, string downloadUrl)
    {
        if (_downloadCache != null)
        {
            var cached = await _downloadCache.TryGetAsync(programInfo.DownloadUrl);
            if (cached != null)
            {
                _logger.LogInformation(
                    "Using cached PRG program {DisplayName} for source URL {SourceUrl} ({ByteCount} bytes)",
                    programInfo.DisplayName,
                    programInfo.DownloadUrl,
                    cached.Length);
                return cached;
            }
        }

        _logger.LogInformation(
            "Downloading PRG program {DisplayName} from source URL {SourceUrl} using request URL {RequestUrl}",
            programInfo.DisplayName,
            programInfo.DownloadUrl,
            downloadUrl);

        using var response = await _httpClient.GetAsync(downloadUrl);
        response.EnsureSuccessStatusCode();
        var prgBytes = await response.Content.ReadAsByteArrayAsync();

        if (_downloadCache != null)
        {
            try
            {
                var etag = response.Headers.ETag?.ToString();
                var lastModified = response.Content.Headers.LastModified?.ToString("o");
                await _downloadCache.PutAsync(programInfo.DownloadUrl, prgBytes, "prg", programInfo.DisplayName, etag, lastModified);
                _logger.LogInformation(
                    "Added PRG program {DisplayName} to download cache from source URL {SourceUrl} ({ByteCount} bytes)",
                    programInfo.DisplayName,
                    programInfo.DownloadUrl,
                    prgBytes.Length);
            }
            catch (Exception ex)
            {
                // Caching is best-effort; a cache write failure must not fail the load.
                _logger.LogWarning(ex, "Failed to cache PRG program {DisplayName} for source URL {SourceUrl}.", programInfo.DisplayName, programInfo.DownloadUrl);
            }
        }

        return prgBytes;
    }
}
