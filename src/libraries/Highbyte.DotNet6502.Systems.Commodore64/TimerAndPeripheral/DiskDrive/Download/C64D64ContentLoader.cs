using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;

public class C64D64ContentLoader : IC64AutoLoadContentLoader
{
    private readonly ILogger _logger;
    private readonly D64Downloader _d64Downloader;

    public C64D64ContentLoader(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        string? corsProxyUrl = null)
    {
        _logger = loggerFactory.CreateLogger(typeof(C64D64ContentLoader).Name);
        _d64Downloader = new D64Downloader(loggerFactory, httpClient, corsProxyUrl);
    }

    public async Task LoadAsync(C64DownloadProgramInfo programInfo, C64 c64)
    {
        var d64Bytes = await _d64Downloader.DownloadAndProcessDiskImage(programInfo);
        await LoadBytesAsync(c64, d64Bytes, programInfo, issueRunCommands: false, _logger);
    }

    public static async Task LoadBytesAsync(
        C64 c64,
        byte[] d64Bytes,
        C64DownloadProgramInfo programInfo,
        bool issueRunCommands,
        ILogger logger)
    {
        var d64DiskImage = D64Parser.ParseD64File(d64Bytes);
        logger.LogInformation("Parsed D64 disk image: {DiskName}", d64DiskImage.DiskName);

        if (!string.IsNullOrEmpty(programInfo.DirectLoadPRGName))
        {
            try
            {
                string directLoadFileName = programInfo.DirectLoadPRGName == "*"
                    ? d64DiskImage.Files.First().FileName
                    : programInfo.DirectLoadPRGName;
                var prgData = d64DiskImage.ReadFileContent(directLoadFileName);

                BinaryLoader.Load(
                    c64.Mem,
                    prgData,
                    out ushort loadedAtAddress,
                    out ushort fileLength);

                logger.LogInformation(
                    "Direct loaded {DirectLoadPRGName} from D64 image at address {LoadedAtAddress:X4}, length {FileLength} bytes",
                    programInfo.DirectLoadPRGName,
                    loadedAtAddress,
                    fileLength);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to direct load PRG file {DirectLoadPRGName}", programInfo.DirectLoadPRGName);
                throw new InvalidOperationException(
                    $"Failed to direct load PRG file {programInfo.DirectLoadPRGName}: {ex.Message}",
                    ex);
            }
        }
        else
        {
            var diskDrive = c64.IECBus?.Devices?.OfType<DiskDrive1541>().FirstOrDefault();
            if (diskDrive == null)
                throw new InvalidOperationException("No DiskDrive1541 found in the running C64 system.");

            diskDrive.SetD64DiskImage(d64DiskImage);
            logger.LogInformation("Disk image loaded and set: {DiskName}", d64DiskImage.DiskName);
        }

        if (issueRunCommands && programInfo.RunCommands.Count > 0)
        {
            logger.LogInformation("Auto-loading and running program with {CommandCount} commands", programInfo.RunCommands.Count);

            foreach (var command in programInfo.RunCommands)
            {
                logger.LogInformation("Executing command: {Command}", command);
                c64.TextPaste.Paste($"{command}\n");

                if (command != programInfo.RunCommands.Last())
                    await Task.Delay(1000);
            }
        }
    }
}
