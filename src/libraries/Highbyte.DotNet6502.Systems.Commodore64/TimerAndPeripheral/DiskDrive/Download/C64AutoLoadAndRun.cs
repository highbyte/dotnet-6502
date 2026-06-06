using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;

public class C64AutoLoadAndRun
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly IHostApp _hostApp;
    private readonly string? _corsProxyUrl;

    public C64AutoLoadAndRun(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        IHostApp hostApp,
        string? corsProxyUrl = null)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(C64AutoLoadAndRun).Name);
        _httpClient = httpClient;
        _hostApp = hostApp;
        _corsProxyUrl = corsProxyUrl;
    }

    public async Task DownloadAndRunProgram(
        C64DownloadProgramInfo programInfo,
        Func<C64DownloadProgramInfo, Task> setConfigCallback)
    {
        var shouldResumeEmulator = false;

        try
        {
            _logger.LogInformation("Resetting C64 emulator before loading program {DisplayName}", programInfo.DisplayName);

            if (_hostApp.EmulatorState == EmulatorState.Running || _hostApp.EmulatorState == EmulatorState.Paused)
            {
                _logger.LogInformation("Stopping C64 emulator to reset state");
                _hostApp.Stop();
            }

            await setConfigCallback(programInfo);

            _logger.LogInformation("Starting C64 emulator");
            await _hostApp.Start();

            var c64 = (C64)_hostApp.CurrentRunningSystem!;

            _logger.LogInformation("Waiting for BASIC to start...");
            var maxWaitTime = TimeSpan.FromSeconds(30);
            var startTime = DateTime.Now;
            var checkInterval = TimeSpan.FromMilliseconds(100);
            while (!c64.HasBasicStarted())
            {
                if (DateTime.Now - startTime > maxWaitTime)
                    throw new TimeoutException("Timeout waiting for BASIC to start");
                await Task.Delay(checkInterval);
            }

            _logger.LogInformation("BASIC has started successfully");
            _logger.LogInformation("Waiting 1 second for BASIC to settle...");
            await Task.Delay(1000);

            _logger.LogInformation("Pausing C64 emulator before program load. State before pause: {EmulatorState}", _hostApp.EmulatorState);
            _hostApp.Pause();
            shouldResumeEmulator = true;

            var contentLoader = CreateContentLoader(programInfo);
            await contentLoader.LoadAsync(programInfo, c64);

            if (programInfo.RunCommands.Count > 0)
            {
                _logger.LogInformation("Auto-loading and running program with {CommandCount} commands", programInfo.RunCommands.Count);

                foreach (var command in programInfo.RunCommands)
                {
                    _logger.LogInformation("Executing command: {Command}", command);
                    c64.TextPaste.Paste($"{command}\n");

                    if (command != programInfo.RunCommands.Last())
                        await Task.Delay(1000);
                }
            }

            await _hostApp.Start();
            shouldResumeEmulator = false;
        }
        catch (Exception ex)
        {
            if (shouldResumeEmulator)
            {
                try
                {
                    _logger.LogInformation("Resuming C64 emulator after auto-download failure. State before resume: {EmulatorState}", _hostApp.EmulatorState);
                    await _hostApp.Start();
                }
                catch (Exception resumeEx)
                {
                    _logger.LogError(resumeEx, "Failed to resume C64 emulator after auto-download failure.");
                }
            }

            _logger.LogError(
                ex,
                "Failed to download and run program {DisplayName}. Details: {ErrorSummary}",
                programInfo.DisplayName,
                DownloadErrorHelper.FlattenExceptionMessages(ex));
            throw;
        }
    }

    private IC64AutoLoadContentLoader CreateContentLoader(C64DownloadProgramInfo programInfo)
        => programInfo.DownloadType switch
        {
            C64DownloadProgramType.D64 or C64DownloadProgramType.D64Zip => new C64D64ContentLoader(
                _loggerFactory,
                _httpClient,
                _corsProxyUrl),
            C64DownloadProgramType.Prg => new C64PrgContentLoader(
                _loggerFactory,
                _httpClient,
                _corsProxyUrl),
            _ => throw new InvalidOperationException($"Unsupported C64 download program type: {programInfo.DownloadType}."),
        };
}
