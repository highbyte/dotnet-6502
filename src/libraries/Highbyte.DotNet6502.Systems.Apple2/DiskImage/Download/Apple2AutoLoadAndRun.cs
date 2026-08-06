using Highbyte.DotNet6502.Systems.Caching;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.DiskImage.Download;

/// <summary>
/// Orchestrates the Apple II "Download &amp; Run" flow, mirroring the C64's
/// <c>C64AutoLoadAndRun</c>: restart the machine for a clean state, wait for Applesoft to boot,
/// download the .dsk (via the shared <see cref="RomDownloader"/> — cache + CORS proxy + ZIP
/// support), extract the chosen catalog file, inject it, and run it.
///
/// Binary (B) files start BRUN-style (PC = load address); Applesoft (A) files are placed at
/// $0801 with the zero-page pointers initialised and started by typing <c>RUN</c> through the
/// text-paste service.
/// </summary>
public class Apple2AutoLoadAndRun
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly RomDownloader _downloader;
    private readonly IHostApp _hostApp;

    private const int BasicBootTimeoutMs = 30_000;
    private const int BasicBootPollMs = 100;

    public Apple2AutoLoadAndRun(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        IHostApp hostApp,
        string? corsProxyUrl = null,
        IDownloadCache? downloadCache = null)
    {
        _logger = loggerFactory.CreateLogger(nameof(Apple2AutoLoadAndRun));
        _loggerFactory = loggerFactory;
        _downloader = new RomDownloader(loggerFactory, httpClient, corsProxyUrl, downloadCache);
        _hostApp = hostApp;
    }

    /// <summary>Downloads the program's disk image and loads + runs the chosen file.</summary>
    public async Task DownloadAndRunProgram(Apple2DownloadProgramInfo programInfo)
    {
        // Restart for a clean machine state, like the C64 flow.
        if (_hostApp.EmulatorState is EmulatorState.Running or EmulatorState.Paused)
        {
            _logger.LogInformation("Stopping emulator before program download.");
            _hostApp.Stop();
        }

        await _hostApp.Start();
        var apple2 = (Apple2System)_hostApp.CurrentRunningSystem!;

        var waited = 0;
        while (!apple2.HasBasicStarted() && waited < BasicBootTimeoutMs)
        {
            await Task.Delay(BasicBootPollMs);
            waited += BasicBootPollMs;
        }
        if (!apple2.HasBasicStarted())
            throw new TimeoutException("Applesoft BASIC did not initialize within 30 seconds.");

        var dskBytes = await _downloader.DownloadRomAsync(
            programInfo.DisplayName,
            new RomDownloadSource(programInfo.DownloadUrl, programInfo.ZipEntryName));

        var diskImage = DskParser.ParseDskFile(dskBytes, _logger);
        await LoadAndRunFileAsync(_hostApp, diskImage, programInfo.FileName, _logger);
    }

    /// <summary>
    /// Loads a catalog file into the running machine and starts it. <paramref name="fileName"/>
    /// <c>"*"</c> (or null/empty) picks the first runnable file. Shared by the download flow and
    /// the host UI's local-image flow.
    /// </summary>
    public static async Task LoadAndRunFileAsync(IHostApp hostApp, DskDiskImage diskImage, string? fileName, ILogger logger)
    {
        if (string.IsNullOrEmpty(fileName) || fileName == "*")
            fileName = diskImage.GetFirstRunnableFileName()
                ?? throw new InvalidOperationException("The disk has no runnable (Binary or Applesoft) file.");

        var entry = diskImage.Files.FirstOrDefault(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"File not found in disk catalog: {fileName}");

        var apple2 = hostApp.CurrentRunningSystem as Apple2System
            ?? throw new InvalidOperationException("The current system is not an Apple II.");

        var wasRunning = hostApp.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();

        try
        {
            switch (entry.FileType)
            {
                case DskFileType.Binary:
                {
                    var fileBytes = diskImage.ReadBinaryFile(entry.FileName);
                    var loadAddress = (ushort)(fileBytes[0] | (fileBytes[1] << 8));
                    BinaryLoader.Load(
                        apple2.Mem,
                        fileBytes[4..],
                        out var loadedAtAddress,
                        out var fileLength,
                        forceLoadAddress: loadAddress);
                    apple2.CPU.PC = loadedAtAddress;   // BRUN semantics
                    logger.LogInformation(
                        "Loaded binary '{FileName}' at {Address} ({Length} bytes), PC set.",
                        entry.FileName, loadedAtAddress.ToHex(), fileLength);
                    break;
                }
                case DskFileType.ApplesoftBasic:
                {
                    var fileBytes = diskImage.ReadApplesoftFile(entry.FileName);
                    BinaryLoader.Load(
                        apple2.Mem,
                        fileBytes,
                        out var loadedAtAddress,
                        out var fileLength,
                        forceLoadAddress: Apple2System.BASIC_LOAD_ADDRESS);
                    apple2.InitBasicMemoryVariables(loadedAtAddress, fileLength);
                    apple2.TextPaste.Paste("RUN\n");
                    logger.LogInformation(
                        "Loaded Applesoft program '{FileName}' ({Length} bytes), RUN queued.",
                        entry.FileName, fileLength);
                    break;
                }
                default:
                    throw new InvalidOperationException(
                        $"'{entry.FileName}' has file type {entry.FileType} — only Binary (B) and Applesoft (A) files can be loaded and run.");
            }
        }
        finally
        {
            if (wasRunning)
                await hostApp.Start();
        }
    }
}
