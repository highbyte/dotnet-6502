using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.ViewModels;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64;

/// <summary>
/// C64 automated-startup participant for the Avalonia host. On the browser host, when an
/// automated (URL-driven) C64 startup finds the C64 ROMs missing, it prompts the user and
/// downloads them before the emulator starts. On desktop it is a no-op — the ROMs are expected
/// to already be on disk, and the normal config UI handles a missing-ROM case (unchanged
/// behaviour).
/// </summary>
/// <remarks>
/// Runs inside <see cref="AutomatedStartupHandler"/> after the system + variant are selected.
/// See <c>docs/automated-startup-abstraction.md</c>.
/// </remarks>
public sealed class C64AvaloniaStartupParticipant : IAutomatedStartupParticipant
{
    private const ushort C64BasicProgramLoadAddress = 0x0801;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public C64AvaloniaStartupParticipant(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger(nameof(C64AvaloniaStartupParticipant));
    }

    public string SystemName => C64.SystemName;

    public async Task<bool> EnsureReadyForStartAsync(IHostApp hostApp, AutomatedStartupRequest request)
    {
        // ROM auto-download applies only to the browser host. On desktop the ROMs are expected to
        // already be on disk; a missing-ROM case is handled by the normal config UI (unchanged).
        if (!OperatingSystem.IsBrowser())
            return true;

        _logger.LogInformation("Checking whether C64 ROMs are available for automated startup.");

        var (isValid, errors) = await hostApp.IsCurrentSystemConfigValid();
        if (isValid || !HasOnlyMissingC64RomErrors(errors))
            return true;

        // C64ConfigDialogViewModel / C64RomPromptService are registered transient — resolve them
        // on demand rather than capturing them in this singleton.
        var romPromptService = _serviceProvider.GetRequiredService<C64RomPromptService>();
        if (!await romPromptService.ShowStartupDownloadPromptAsync())
        {
            _logger.LogInformation("User cancelled automated C64 ROM download prompt.");
            return false;
        }

        var configViewModel = _serviceProvider.GetRequiredService<C64ConfigDialogViewModel>();
        if (!await configViewModel.DownloadRomsToByteArrayAsync(requireAcknowledgement: false))
        {
            _logger.LogError("Automatic C64 ROM download failed: {StatusMessage}",
                configViewModel.StatusMessage ?? "Unknown error.");
            return false;
        }

        if (!await configViewModel.TryApplyChangesAsync())
        {
            _logger.LogError("Automatic C64 ROM download could not be saved: {StatusMessage} {ValidationMessage}",
                configViewModel.StatusMessage ?? string.Empty,
                configViewModel.ValidationMessage ?? string.Empty);
            return false;
        }

        var (isValidAfterDownload, errorsAfterDownload) = await hostApp.IsCurrentSystemConfigValid();
        if (!isValidAfterDownload)
        {
            _logger.LogError("C64 configuration is still invalid after automatic ROM download: {Errors}",
                string.Join(" | ", errorsAfterDownload));
            return false;
        }

        _logger.LogInformation("C64 ROMs were downloaded automatically for browser startup.");
        return true;
    }

    private static bool HasOnlyMissingC64RomErrors(IReadOnlyCollection<string> errors)
        => errors.Count > 0 && errors.All(error => error.StartsWith("Missing ROMs:", StringComparison.Ordinal));

    /// <summary>
    /// Post-ready automation: if the request carries the C64 BASIC parameters (<c>basicText</c> /
    /// <c>basicUrl</c> / <c>runBasic</c>), paste the BASIC source into the running C64 once the
    /// BASIC prompt is ready. Invalid combinations are logged and skipped — the emulator keeps
    /// running. See <c>docs/automated-startup-seam2.md</c>.
    /// </summary>
    public async Task OnSystemReadyAsync(
        IHostApp hostApp, AutomatedStartupRequest request, AutomatedStartupContext context)
    {
        var extras = request.ExtraParameters;
        var basicText = GetExtra(extras, "basicText");
        var basicUrl = GetExtra(extras, "basicUrl");
        var runBasic = extras.TryGetValue("runBasic", out var rb) && IsTruthy(rb);

        if (basicText is null && basicUrl is null)
            return;   // no BASIC automation requested

        if (basicText is not null && basicUrl is not null)
        {
            _logger.LogWarning("Automation 'basicText' and 'basicUrl' are mutually exclusive; skipping BASIC source.");
            return;
        }
        if (!request.AutoStart || !request.WaitForSystemReady)
        {
            _logger.LogWarning("Automation 'basicText'/'basicUrl' require the system to be started and ready (start + waitForSystemReady); skipping BASIC source.");
            return;
        }
        if (request.LoadPrgPath is not null)
        {
            _logger.LogWarning("Automation 'basicText'/'basicUrl' are mutually exclusive with loading a PRG; skipping BASIC source.");
            return;
        }

        // Resolve the BASIC source: inline base64url 'basicText', or text fetched from 'basicUrl'.
        string? basicSource;
        if (basicText is not null)
        {
            basicSource = TryDecodeBase64UrlUtf8(basicText);
            if (basicSource is null)
            {
                _logger.LogWarning("Automation 'basicText' is not valid base64url; skipping BASIC source.");
                return;
            }
        }
        else
        {
            if (context.FetchTextResource is null)
            {
                _logger.LogWarning("Automation 'basicUrl' was supplied but the host provides no text-resource fetcher; skipping BASIC source.");
                return;
            }
            try
            {
                basicSource = await context.FetchTextResource(basicUrl!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch automation 'basicUrl' '{BasicUrl}'; skipping BASIC source.", basicUrl);
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(basicSource))
        {
            _logger.LogWarning("Automation BASIC source is empty; skipping.");
            return;
        }

        if (hostApp.CurrentRunningSystem is not C64 c64)
        {
            _logger.LogError("Automation BASIC source requires the running system to be C64.");
            return;
        }

        var pasteText = BuildC64BasicPasteText(basicSource, runBasic);
        _logger.LogInformation("Queueing C64 BASIC source ({LineCount} line(s), runBasic={RunBasic}).",
            CountNonEmptyLines(basicSource), runBasic);
        c64.TextPaste.Paste(pasteText);
    }

    public async Task BeforePrgLoadAsync(
        IHostApp hostApp,
        AutomatedStartupRequest request,
        AutomatedStartupContext context)
    {
        if (!request.WaitForSystemReady || request.LoadPrgPath is null)
            return;

        if (hostApp.CurrentRunningSystem is not C64)
            return;

        // Match the existing D64 autorun flow: BASIC reports ready once TXTAB is initialized,
        // but an extra moment is needed before direct PRG loads reliably stick.
        _logger.LogInformation("Waiting 1 second for C64 BASIC to settle before automated PRG load.");
        await Task.Delay(1000);
    }

    public Task OnPrgLoadedAsync(
        IHostApp hostApp,
        AutomatedStartupRequest request,
        AutomatedStartupContext context,
        ushort loadAddress,
        ushort fileLength)
    {
        if (loadAddress != C64BasicProgramLoadAddress)
            return Task.CompletedTask;

        if (hostApp.CurrentRunningSystem is not C64 c64)
        {
            _logger.LogError("Loaded C64 BASIC PRG requires the running system to be C64.");
            return Task.CompletedTask;
        }

        c64.InitBasicMemoryVariables(loadAddress, fileLength);
        _logger.LogInformation(
            "Initialized C64 BASIC memory variables for loaded PRG at 0x{LoadAddress:X4}, length {FileLength}.",
            loadAddress,
            fileLength);
        return Task.CompletedTask;
    }

    public Task<bool> TryRunLoadedProgramAsync(
        IHostApp hostApp,
        AutomatedStartupRequest request,
        AutomatedStartupContext context,
        ushort loadAddress)
    {
        if (loadAddress != C64BasicProgramLoadAddress)
            return Task.FromResult(false);

        if (!request.WaitForSystemReady)
        {
            _logger.LogWarning(
                "Running a loaded C64 BASIC program requires waitForSystemReady; falling back to generic PRG start.");
            return Task.FromResult(false);
        }

        if (hostApp.CurrentRunningSystem is not C64 c64)
        {
            _logger.LogError("Running a loaded C64 BASIC program requires the running system to be C64.");
            return Task.FromResult(false);
        }

        _logger.LogInformation("Queueing C64 BASIC RUN command for loaded PRG at 0x{LoadAddress:X4}.", loadAddress);
        c64.TextPaste.Paste("run\n");
        return Task.FromResult(true);
    }

    private static string? GetExtra(IReadOnlyDictionary<string, string> extras, string key)
        => extras.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static bool IsTruthy(string value)
        => value.Length == 0
        || value.Equals("1", StringComparison.Ordinal)
        || value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>Decodes a base64url-encoded UTF-8 string (RFC 4648 §5; padding optional). Returns null if invalid.</summary>
    private static string? TryDecodeBase64UrlUtf8(string base64Url)
    {
        var standard = base64Url.Replace('-', '+').Replace('_', '/');
        switch (standard.Length % 4)
        {
            case 2: standard += "=="; break;
            case 3: standard += "="; break;
            case 1: return null;
        }
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(standard));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Normalises line endings of C64 BASIC source for keyboard paste, ensures a trailing newline,
    /// and appends a lower-case <c>run</c> + Return when requested. (Lower-case: the C64 keyboard
    /// buffer expects unshifted characters — an upper-case <c>RUN</c> arrives as graphic glyphs.)
    /// </summary>
    private static string BuildC64BasicPasteText(string basicSource, bool runBasic)
    {
        var normalized = basicSource.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        if (!normalized.EndsWith("\n", StringComparison.Ordinal))
            normalized += "\n";
        if (runBasic)
            normalized += "run\n";
        return normalized;
    }

    private static int CountNonEmptyLines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Length;
}
