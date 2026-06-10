using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;
using Highbyte.DotNet6502.Utils;
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

    // Extra-parameter keys shared by Desktop and Browser (Desktop: --loadD64 etc.; Browser: query
    // params loadD64Url etc. pre-fetched into d64BytesB64). The participant never sees raw HTTP /
    // CLI parsing — only these typed extras.
    internal const string ExtraKeyLoadD64Path = "loadD64Path";
    internal const string ExtraKeyLoadD64Url = "loadD64Url";
    internal const string ExtraKeyD64Program = "d64Program";
    internal const string ExtraKeyDiskMount = "diskMount";
    internal const string ExtraKeyKeyboardJoystickEnabled = "keyboardJoystickEnabled";
    internal const string ExtraKeyKeyboardJoystickNumber = "keyboardJoystickNumber";
    internal const string ExtraKeyAudioEnabled = "audioEnabled";

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
        // Apply any C64-runtime config supplied via extras (joystick / audio) onto the live
        // host config before the system starts. Done first so the ROM-download branch below sees
        // the same config the user requested.
        ApplyRuntimeConfigFromExtras(hostApp, request);

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
        var configViewModel = _serviceProvider.GetRequiredService<C64ConfigDialogViewModel>();
        if (!await romPromptService.RunStartupDownloadWorkflowAsync(
            configViewModel,
            async () =>
            {
                if (!await configViewModel.TryApplyChangesAsync())
                {
                    var failureMessage = string.Join(
                        Environment.NewLine,
                        new[]
                        {
                            configViewModel.StatusMessage,
                            configViewModel.ValidationMessage
                        }.Where(message => !string.IsNullOrWhiteSpace(message)));
                    if (string.IsNullOrWhiteSpace(failureMessage))
                        failureMessage = "The downloaded ROM configuration could not be saved.";

                    _logger.LogError("Automatic C64 ROM download could not be saved: {StatusMessage} {ValidationMessage}",
                        configViewModel.StatusMessage ?? string.Empty,
                        configViewModel.ValidationMessage ?? string.Empty);
                    return (false, "C64 ROM Configuration Failed", failureMessage);
                }

                var (isValidAfterDownload, errorsAfterDownload) = await hostApp.IsCurrentSystemConfigValid();
                if (!isValidAfterDownload)
                {
                    var failureMessage = string.Join(Environment.NewLine, errorsAfterDownload);
                    _logger.LogError("C64 configuration is still invalid after automatic ROM download: {Errors}",
                        string.Join(" | ", errorsAfterDownload));
                    return (false, "C64 Configuration Still Invalid", failureMessage);
                }

                return (true, null, null);
            }))
        {
            _logger.LogInformation("Automated C64 ROM download workflow was cancelled or failed.");
            return false;
        }

        _logger.LogInformation("C64 ROMs were downloaded automatically for browser startup.");
        return true;
    }

    private static bool HasOnlyMissingC64RomErrors(IReadOnlyCollection<string> errors)
        => errors.Count > 0 && errors.All(error => error.StartsWith("Missing ROMs:", StringComparison.Ordinal));

    /// <summary>
    /// Post-ready automation: paste C64 BASIC source (<c>basicText</c> / <c>basicUrl</c>), or run
    /// the .d64 startup flow (<c>loadD64Path</c> / <c>d64BytesB64</c>) — mount the disk or
    /// direct-load a PRG and optionally paste the run commands. Invalid combinations are logged
    /// and skipped; the emulator keeps running. See <c>docs/automated-startup-seam2.md</c>.
    /// </summary>
    public async Task OnSystemReadyAsync(
        IHostApp hostApp, AutomatedStartupRequest request, AutomatedStartupContext context)
    {
        var extras = request.ExtraParameters;
        await TryHandleBasicAutomationAsync(hostApp, request, context, extras);
        await TryHandleD64AutomationAsync(hostApp, request, context, extras);
    }

    private async Task TryHandleBasicAutomationAsync(
        IHostApp hostApp,
        AutomatedStartupRequest request,
        AutomatedStartupContext context,
        IReadOnlyDictionary<string, string> extras)
    {
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

    private async Task TryHandleD64AutomationAsync(
        IHostApp hostApp,
        AutomatedStartupRequest request,
        AutomatedStartupContext context,
        IReadOnlyDictionary<string, string> extras)
    {
        var loadD64Path = GetExtra(extras, ExtraKeyLoadD64Path);
        var loadD64Url = GetExtra(extras, ExtraKeyLoadD64Url);
        if (loadD64Path is null && loadD64Url is null)
            return;

        if (!request.AutoStart || !request.WaitForSystemReady)
        {
            _logger.LogWarning("Automation 'loadD64Path'/'loadD64Url' require start + waitForSystemReady; skipping .d64 startup.");
            return;
        }
        if (request.LoadPrgPath is not null)
        {
            _logger.LogWarning("Automation .d64 load is mutually exclusive with loading a PRG; skipping .d64 startup.");
            return;
        }
        if (hostApp.CurrentRunningSystem is not C64 c64)
        {
            _logger.LogError(".d64 startup requires the running system to be C64.");
            return;
        }

        var d64Program = GetExtra(extras, ExtraKeyD64Program);
        var diskMount = extras.TryGetValue(ExtraKeyDiskMount, out var dm) && IsTruthy(dm);
        if (d64Program is null && !diskMount)
        {
            _logger.LogWarning("Automation .d64 load requires exactly one of 'd64Program' or 'diskMount'; skipping.");
            return;
        }
        if (d64Program is not null && diskMount)
        {
            _logger.LogWarning("Automation 'd64Program' and 'diskMount' are mutually exclusive; skipping .d64 startup.");
            return;
        }
        if (loadD64Url is not null && context.FetchBinaryResource is null)
        {
            _logger.LogWarning("Automation 'loadD64Url' supplied but host provides no binary-resource fetcher; skipping .d64 startup.");
            return;
        }

        // Resolve the .d64 bytes: local file (Desktop) or fetched via the host's binary-resource
        // fetcher (Browser HTTP). The fetch runs here — after the C64 is started and BASIC reports
        // ready — so the user sees the live BASIC prompt while a remote .d64 downloads, rather than
        // a blank Avalonia window during a pre-Avalonia download.
        byte[] d64Bytes;
        try
        {
            d64Bytes = loadD64Url is not null
                ? await context.FetchBinaryResource!(loadD64Url)
                : await ResolveD64BytesFromFileAsync(loadD64Path!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read .d64 bytes; skipping .d64 startup.");
            return;
        }
        if (d64Bytes.Length == 0)
        {
            _logger.LogWarning(".d64 bytes resolved to an empty buffer; skipping .d64 startup.");
            return;
        }

        // The fetched bytes may be a raw .d64 or a ZIP archive containing one (a shared link
        // points at the program's original download URL, which is often a .zip). The query
        // contract carries no download-type hint, so content-sniff and extract if needed.
        try
        {
            d64Bytes = D64ZipExtractor.EnsureD64Bytes(d64Bytes, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract .d64 from the downloaded ZIP archive; skipping .d64 startup.");
            return;
        }

        // Match the existing menu's download flow: an extra second after BASIC reports ready
        // before mutating disk state, so keystrokes and direct loads land reliably.
        _logger.LogInformation("Waiting 1 second for C64 BASIC to settle before .d64 startup.");
        await Task.Delay(1000);

        // The menu's wrapper pauses the C64 around the inner mount/direct-load + paste so the
        // RAM/disk-drive mutations happen against a quiescent CPU. Mirror that here, then
        // resume so the queued keystrokes (LOAD"*",8,1 / RUN) get processed.
        var pausedForD64 = false;
        if (hostApp.EmulatorState == EmulatorState.Running)
        {
            _logger.LogInformation("Pausing C64 before .d64 mount / direct-load.");
            hostApp.Pause();
            pausedForD64 = hostApp.EmulatorState == EmulatorState.Paused;
        }

        try
        {
            var programInfo = BuildProgramInfo(extras, d64Program, loadD64Path);
            await C64D64ContentLoader.LoadBytesAsync(
                c64,
                d64Bytes,
                programInfo,
                issueRunCommands: request.RunLoadedProgram,
                _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during .d64 mount / direct-load; emulator left running.");
        }
        finally
        {
            if (pausedForD64 && hostApp.EmulatorState == EmulatorState.Paused)
            {
                _logger.LogInformation("Resuming C64 after .d64 mount / direct-load.");
                await hostApp.Start();
            }
        }
    }

    private static async Task<byte[]> ResolveD64BytesFromFileAsync(string loadD64Path)
    {
        var expanded = PathHelper.ExpandOSEnvironmentVariables(loadD64Path);
        if (!File.Exists(expanded))
            throw new FileNotFoundException($".d64 file not found: {expanded}", expanded);
        return await File.ReadAllBytesAsync(expanded);
    }

    private static C64DownloadProgramInfo BuildProgramInfo(
        IReadOnlyDictionary<string, string> extras,
        string? d64Program,
        string? loadD64Path)
    {
        // displayName: just for logging — use the file basename when present, else a generic label.
        var displayName = !string.IsNullOrEmpty(loadD64Path)
            ? Path.GetFileNameWithoutExtension(loadD64Path)
            : "Startup .d64";

        int keyboardJoystickNumber = 2;
        if (extras.TryGetValue(ExtraKeyKeyboardJoystickNumber, out var kjnRaw)
            && int.TryParse(kjnRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kjn))
        {
            keyboardJoystickNumber = kjn;
        }
        var keyboardJoystickEnabled =
            extras.TryGetValue(ExtraKeyKeyboardJoystickEnabled, out var kjeRaw) && IsTruthy(kjeRaw);

        var audioEnabled =
            extras.TryGetValue(ExtraKeyAudioEnabled, out var aeRaw) && IsTruthy(aeRaw);

        // Direct-load when --d64Program supplied (incl. "*"); otherwise mount the disk.
        // DownloadUrl is unused on the startup path (bytes are already in hand) but the DTO
        // requires a non-null string.
        return new C64DownloadProgramInfo(
            displayName: displayName,
            downloadUrl: loadD64Path ?? string.Empty,
            keyboardJoystickEnabled: keyboardJoystickEnabled,
            keyboardJoystickNumber: keyboardJoystickNumber,
            audioEnabled: audioEnabled,
            directLoadPRGName: d64Program);
    }

    /// <summary>
    /// Apply any of the C64-runtime extras (joystick on/off, joystick port, audio enable) onto the
    /// resolved <see cref="C64HostConfig"/> so they take effect when the system starts. Only fields
    /// the user actually supplied are touched. These knobs are independent of the <c>.d64</c>
    /// startup flow — they apply whenever the selected system is C64 (e.g. plain
    /// <c>--system C64 --start</c>, <c>--loadPrg</c>, BASIC paste, or <c>--loadD64</c>).
    /// </summary>
    private void ApplyRuntimeConfigFromExtras(IHostApp hostApp, AutomatedStartupRequest request)
    {
        var extras = request.ExtraParameters;
        var hasKeyboardJoystickEnabled = extras.ContainsKey(ExtraKeyKeyboardJoystickEnabled);
        var hasKeyboardJoystickNumber = extras.ContainsKey(ExtraKeyKeyboardJoystickNumber);
        var hasAudioEnabled = extras.ContainsKey(ExtraKeyAudioEnabled);
        if (!hasKeyboardJoystickEnabled && !hasKeyboardJoystickNumber && !hasAudioEnabled)
            return;

        if (hostApp.CurrentHostSystemConfig is not C64HostConfig c64HostConfig)
        {
            _logger.LogWarning("Current host config is not C64HostConfig; cannot apply C64 runtime-config extras.");
            return;
        }

        var systemConfig = c64HostConfig.SystemConfig;
        var inputConfig = c64HostConfig.InputConfig;
        var changed = false;

        if (hasKeyboardJoystickNumber)
        {
            if (int.TryParse(extras[ExtraKeyKeyboardJoystickNumber], NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
                && (port == 1 || port == 2))
            {
                systemConfig.KeyboardJoystick = port;
                // Match the menu's "Download and Run" flow: keep the active gamepad/joystick port
                // in sync with the keyboard-joystick port so both input sources drive the same C64 port.
                inputConfig.CurrentJoystick = port;
                // The number alone implies enable, mirroring the design ("Implies
                // --keyboardJoystickEnabled if not also given").
                systemConfig.KeyboardJoystickEnabled = true;
                changed = true;
            }
            else
            {
                _logger.LogWarning("Automation '{Key}' must be 1 or 2; ignoring.", ExtraKeyKeyboardJoystickNumber);
            }
        }

        if (hasKeyboardJoystickEnabled)
        {
            systemConfig.KeyboardJoystickEnabled = IsTruthy(extras[ExtraKeyKeyboardJoystickEnabled]);
            changed = true;
        }

        if (hasAudioEnabled)
        {
            systemConfig.AudioEnabled = IsTruthy(extras[ExtraKeyAudioEnabled]);
            changed = true;
        }

        if (changed)
        {
            hostApp.UpdateHostSystemConfig(c64HostConfig);
            _logger.LogInformation(
                "Applied C64 runtime config from automation extras (KeyboardJoystickEnabled={Kje}, KeyboardJoystick={Kj}, AudioEnabled={Ae}).",
                systemConfig.KeyboardJoystickEnabled, systemConfig.KeyboardJoystick, systemConfig.AudioEnabled);
        }
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
