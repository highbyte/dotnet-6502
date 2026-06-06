using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;

public class C64PrgContentLoader : IC64AutoLoadContentLoader
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _corsProxyUrl;

    public C64PrgContentLoader(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        string? corsProxyUrl = null)
    {
        _logger = loggerFactory.CreateLogger(typeof(C64PrgContentLoader).Name);
        _httpClient = httpClient;
        _corsProxyUrl = corsProxyUrl;
    }

    public async Task LoadAsync(C64DownloadProgramInfo programInfo, C64 c64)
    {
        var downloadUrl = _corsProxyUrl != null
            ? _corsProxyUrl + Uri.EscapeDataString(programInfo.DownloadUrl)
            : programInfo.DownloadUrl;

        _logger.LogInformation(
            "Downloading PRG program {DisplayName} from source URL {SourceUrl} using request URL {RequestUrl}",
            programInfo.DisplayName,
            programInfo.DownloadUrl,
            downloadUrl);

        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();
            var prgBytes = await response.Content.ReadAsByteArrayAsync();

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
}
