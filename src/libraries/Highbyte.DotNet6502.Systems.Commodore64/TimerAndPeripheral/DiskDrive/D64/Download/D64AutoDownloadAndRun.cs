using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;

public class D64AutoDownloadAndRun
{
    private readonly ILogger _logger;
    private readonly D64Downloader _d64Downloader;
    private readonly IHostApp _hostApp;


    public D64AutoDownloadAndRun(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        IHostApp hostApp,
        string? corsProxyUrl = null)
    {
        _logger = loggerFactory.CreateLogger(typeof(D64AutoDownloadAndRun).Name);
        _d64Downloader = new D64Downloader(loggerFactory, httpClient, corsProxyUrl);
        _hostApp = hostApp;
    }

    public async Task DownloadAndRunDiskImage(
        D64DownloadDiskInfo diskInfo,
        Func<D64DownloadDiskInfo, Task> setConfigCallback)
    {
        try
        {
            // First reset the C64 emulator
            _logger.LogInformation("Resetting C64 emulator before loading disk image");

            if (_hostApp.EmulatorState == EmulatorState.Running || _hostApp.EmulatorState == EmulatorState.Paused)
            {
                _logger.LogInformation("Stopping C64 emulator to reset state");
                _hostApp.Stop();
            }

            await setConfigCallback(diskInfo);

            // Start the C64 emulator
            _logger.LogInformation("Starting C64 emulator");
            await _hostApp.Start();

            C64 c64 = (C64)_hostApp.CurrentRunningSystem!;

            // Wait for BASIC to start (check periodically)
            _logger.LogInformation("Waiting for BASIC to start...");
            var maxWaitTime = TimeSpan.FromSeconds(30);
            var startTime = DateTime.Now;
            var checkInterval = TimeSpan.FromMilliseconds(100);
            while (!c64.HasBasicStarted())
            {
                if (DateTime.Now - startTime > maxWaitTime)
                {
                    throw new TimeoutException("Timeout waiting for BASIC to start");
                }
                await Task.Delay(checkInterval);
            }
            _logger.LogInformation("BASIC has started successfully");
            // Add a 1-second delay to allow BASIC to settle before proceeding
            _logger.LogInformation("Waiting 1 second for BASIC to settle...");
            await Task.Delay(1000);

            // Pause before proceeding with disk operations
            _logger.LogInformation($"Pausing C64 emulator before disk operations. State before pause: {_hostApp.EmulatorState}");
            _hostApp.Pause();

            // Download and process the disk image (supports both .d64 and .zip files)
            var d64Bytes = await _d64Downloader.DownloadAndProcessDiskImage(diskInfo);

            // Parse the D64 disk image
            var d64DiskImage = D64Parser.ParseD64File(d64Bytes);
            _logger.LogInformation($"Parsed D64 disk image: {d64DiskImage.DiskName}");

            // Check if direct PRG loading is requested (will bypass the disk drive emulation, and extract a PRG file from the .D64 image and load it directly into emulator memory)
            if (!string.IsNullOrEmpty(diskInfo.DirectLoadPRGName))
            {
                _logger.LogInformation($"Direct loading PRG file: {diskInfo.DirectLoadPRGName}");

                // Extract the specified file data directly from the D64 image
                try
                {
                    string directLoadFileName = diskInfo.DirectLoadPRGName == "*" ? d64DiskImage.Files.First().FileName : diskInfo.DirectLoadPRGName;
                    var prgData = d64DiskImage.ReadFileContent(directLoadFileName);
                    _logger.LogInformation($"Successfully extracted file {diskInfo.DirectLoadPRGName}, size: {prgData.Length} bytes");

                    // Load the file data directly into memory using BinaryLoader
                    BinaryLoader.Load(
                        c64.Mem,
                        prgData,
                        out ushort loadedAtAddress,
                        out ushort fileLength);

                    _logger.LogInformation($"Direct loaded {diskInfo.DirectLoadPRGName} at address {loadedAtAddress:X4}, length {fileLength} bytes");

                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to direct load PRG file {diskInfo.DirectLoadPRGName}: {ex.Message}");
                    throw new InvalidOperationException($"Failed to direct load PRG file {diskInfo.DirectLoadPRGName}: {ex.Message}", ex);
                }
            }
            else
            {
                // Set the disk image on the running C64's DiskDrive1541. Let the files be loaded from the disk image via BASIC commands (below).
                var diskDrive = c64.IECBus?.Devices?.OfType<DiskDrive1541>().FirstOrDefault();
                if (diskDrive != null)
                {
                    diskDrive.SetD64DiskImage(d64DiskImage);
                    _logger.LogInformation($"Disk image loaded and set: {d64DiskImage.DiskName}");
                }
                else
                {
                    throw new InvalidOperationException("No DiskDrive1541 found in the running C64 system.");
                }
            }

            // Run the the specified Basic commands.
            // Note: If DirectLoadPRGName was specified, RunCommands must not include a LOAD command, only typically a RUN command.
            if (diskInfo.RunCommands != null && diskInfo.RunCommands.Count > 0)
            {
                _logger.LogInformation($"Auto-loading and running program with {diskInfo.RunCommands.Count} commands");

                // Execute each command in sequence
                foreach (var command in diskInfo.RunCommands)
                {
                    _logger.LogInformation($"Executing command: {command}");
                    c64.TextPaste.Paste($"{command}\n");

                    // Wait a moment between commands (except for the last one)
                    if (command != diskInfo.RunCommands.Last())
                    {
                        await Task.Delay(1000);
                    }
                }
            }

            await _hostApp.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Load error: {ex.Message}");
            throw;
        }
    }
}
