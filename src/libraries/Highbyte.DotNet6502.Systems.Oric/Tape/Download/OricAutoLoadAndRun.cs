using Highbyte.DotNet6502.Systems.Caching;
using Microsoft.Extensions.Logging;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Tape.Download;

/// <summary>
/// Downloads, inserts, and starts an Oric byte-level TAP image through the standard Atmos ROM
/// cassette routines. The shared downloader supplies caching, Browser CORS-proxy routing, and
/// named ZIP extraction.
/// </summary>
public sealed class OricAutoLoadAndRun
{
    private const int BasicBootTimeoutMs = 30_000;
    private const int BasicBootPollMs = 100;

    private readonly ILogger _logger;
    private readonly RomDownloader _downloader;
    private readonly IHostApp _hostApp;

    public OricAutoLoadAndRun(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        IHostApp hostApp,
        string? corsProxyUrl = null,
        IDownloadCache? downloadCache = null)
    {
        _logger = loggerFactory.CreateLogger(nameof(OricAutoLoadAndRun));
        _downloader = new RomDownloader(loggerFactory, httpClient, corsProxyUrl, downloadCache);
        _hostApp = hostApp;
    }

    public Task DownloadAndRunProgram(
        OricDownloadProgramInfo programInfo,
        CancellationToken cancellationToken = default)
        => DownloadAndRunProgram(
            programInfo,
            static _ => Task.CompletedTask,
            cancellationToken);

    public async Task DownloadAndRunProgram(
        OricDownloadProgramInfo programInfo,
        Func<OricDownloadProgramInfo, Task> setConfigCallback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(programInfo);
        ArgumentNullException.ThrowIfNull(setConfigCallback);

        if (_hostApp.EmulatorState is EmulatorState.Running or EmulatorState.Paused)
        {
            _logger.LogInformation("Stopping emulator before Oric program download.");
            _hostApp.Stop();
        }

        await setConfigCallback(programInfo);
        await _hostApp.Start();
        var oric = _hostApp.CurrentRunningSystem as OricMachine
            ?? throw new InvalidOperationException("The current system is not an Oric Atmos.");

        await WaitForBasicAsync(oric, cancellationToken);

        var tapBytes = await _downloader.DownloadRomAsync(
            programInfo.DisplayName,
            new RomDownloadSource(programInfo.DownloadUrl, programInfo.ZipEntryName),
            cancellationToken);
        var firstFile = OricTapParser.Parse(tapBytes);
        var loadAndRunText = BuildLoadAndRunText(firstFile);

        var wasRunning = _hostApp.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            _hostApp.Pause();

        try
        {
            var files = oric.InsertTape(tapBytes);
            oric.TextPaste.Paste(loadAndRunText);
            _logger.LogInformation(
                "Inserted Oric TAP for {ProgramName}: {FileCount} file(s), {ByteCount} bytes; CLOAD queued.",
                programInfo.DisplayName,
                files.Count,
                tapBytes.Length);
        }
        finally
        {
            if (wasRunning)
                await _hostApp.Start();
        }
    }

    private static string BuildLoadAndRunText(OricTapFile firstFile)
    {
        const string loadCommand = "CLOAD\"\"\n";
        if (firstFile.IsAutoRun)
            return loadCommand;

        return firstFile.IsBasic
            ? loadCommand + "RUN\n"
            : loadCommand + $"CALL#{firstFile.StartAddress:X4}\n";
    }

    private static async Task WaitForBasicAsync(OricMachine oric, CancellationToken cancellationToken)
    {
        var waited = 0;
        while (!oric.IsSystemReady() && waited < BasicBootTimeoutMs)
        {
            await Task.Delay(BasicBootPollMs, cancellationToken);
            waited += BasicBootPollMs;
        }

        if (!oric.IsSystemReady())
            throw new TimeoutException("Oric Extended BASIC did not initialize within 30 seconds.");
    }
}
