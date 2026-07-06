using Highbyte.DotNet6502.Updates;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>
/// Terminal-host wrapper around the shared update flow. Runs the (gated) update check off the UI
/// thread, holds the latest result for the in-TUI Updates dialog and the "update available" indicator,
/// and — when the user picks "Update now" — records a pending upgrade that the app runs after the TUI
/// has released the terminal (so the package manager's output is visible on a normal console).
/// </summary>
public sealed class TerminalUpdateService
{
    private readonly AppUpdateDescriptor _descriptor;
    private readonly bool _updateCheckEnabled;
    private readonly ILogger _logger;

    public TerminalUpdateService(AppUpdateDescriptor descriptor, bool updateCheckEnabled, ILoggerFactory loggerFactory)
    {
        _descriptor = descriptor;
        _updateCheckEnabled = updateCheckEnabled;
        _logger = loggerFactory.CreateLogger("UpdateCheck");
    }

    /// <summary>Latest check result, or null until the first check has completed.</summary>
    public UpdateCheckResult? Latest { get; private set; }

    public bool IsUpdateAvailable => Latest?.IsUpdateAvailable == true;

    /// <summary>Whether a one-click in-app update is possible right now (managed install + update available).</summary>
    public bool CanUpdateNow => IsUpdateAvailable && Latest?.ManagerExecutablePath is not null;

    /// <summary>True once "Update now" was chosen; the app runs the upgrade after the TUI exits.</summary>
    public bool PendingUpdateRequested { get; private set; }

    /// <summary>Raised (possibly off the UI thread) whenever <see cref="Latest"/> changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Runs an update check. The automatic (non-forced) check is gated by the <c>UpdateCheckEnabled</c>
    /// setting and the CI / opt-out env suppressors; a forced check (the dialog's "Check now") ignores
    /// both. On update-available the underlying checker logs one line via the injected logger (routed to
    /// the TUI's Logs pane). Never throws — a failed check must not disrupt the app.
    /// </summary>
    public async Task CheckAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && (!_updateCheckEnabled || ConsoleUpdateCli.IsSuppressedByEnvironment()))
            return;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var checker = UpdateChecker.CreateDefault(_descriptor, http, _logger);
            Latest = await checker.CheckAsync(new UpdateCheckContext { ForceCheck = force }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed.");
        }

        Changed?.Invoke();
    }

    /// <summary>Records that the user asked to update now (consumed by the app after the TUI exits).</summary>
    public void RequestUpdateNow()
    {
        if (CanUpdateNow)
            PendingUpdateRequested = true;
    }

    /// <summary>
    /// Runs the recorded pending upgrade in the foreground; call only after the TUI has released the
    /// terminal so the package manager's output is visible. Returns true on success, false if nothing
    /// is pending or the upgrade failed.
    /// </summary>
    public async Task<bool> RunPendingUpdateAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        if (!PendingUpdateRequested || Latest?.ManagerExecutablePath is null)
            return false;

        return await UpdateApplier.RunUpgradeAsync(
            Latest.ManagerExecutablePath, _descriptor.UpgradeArgs(Latest.Channel), output, cancellationToken).ConfigureAwait(false);
    }
}
