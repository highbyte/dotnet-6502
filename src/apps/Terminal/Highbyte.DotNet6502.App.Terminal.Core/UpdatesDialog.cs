using Highbyte.DotNet6502.Updates;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

// Static Application API (Run for the modal dialog) is obsolete in Terminal.Gui 2.4.5 but functional.
#pragma warning disable CS0618

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>
/// Modal "Updates" dialog for the terminal host, opened with the leader-key <c>U</c> command. Shows the
/// current version and update status, and — on a package-manager install with a newer release — the
/// suggested <c>brew</c>/<c>scoop</c> command plus an "Update now" action. "Update now" records the
/// request and quits the TUI; the app then runs the upgrade in the foreground. This keeps the update
/// affordance inside the TUI (behind the leader key) so it never interferes with emulator keyboard focus.
/// </summary>
internal static class UpdatesDialog
{
    public static void Show(TuiHostApp host, TerminalUpdateService service)
    {
        var dialog = new Window
        {
            Title = "Updates  (Esc to close)",
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = 64,
            Height = 12,
            BorderStyle = LineStyle.Single,
        };
        host.ApplyUiScheme(dialog);

        var versionLabel = new Label { X = 0, Y = 0, Width = Dim.Fill(), Text = "" };
        var statusLabel = new Label { X = 0, Y = 1, Width = Dim.Fill(), Height = 2, Text = "" };

        // Suggested upgrade command (read-only, selectable to copy), shown only when an update is available.
        var commandField = new TextField { X = 0, Y = 3, Width = Dim.Fill(), ReadOnly = true, Visible = false };

        var checkButton = new Button { X = 0, Y = Pos.AnchorEnd(1), Text = "Check now" };
        var updateButton = new Button { X = Pos.Right(checkButton) + 1, Y = Pos.AnchorEnd(1), Text = "Update now", Visible = false };
        var closeButton = new Button { X = Pos.AnchorEnd(9), Y = Pos.AnchorEnd(1), Text = "Close", IsDefault = true };

        dialog.Add(versionLabel, statusLabel, commandField, checkButton, updateButton, closeButton);

        var closed = false;

        void Refresh()
        {
            var current = AppVersion.GetCurrent();
            versionLabel.Text = $"Version: {(current is null ? "development build (unversioned)" : "v" + current)}";

            var result = service.Latest;
            statusLabel.Text = StatusText(result);

            var hasCommand = result?.IsUpdateAvailable == true && !string.IsNullOrEmpty(result.SuggestedCommand);
            commandField.Visible = hasCommand;
            if (hasCommand)
                commandField.Text = result!.SuggestedCommand ?? string.Empty;

            // "Update now" only when the package manager was resolved (a one-click upgrade is possible).
            updateButton.Visible = service.CanUpdateNow;

            dialog.SetNeedsDraw();
        }

        // Refresh live when a background check (the automatic one, or "Check now") completes.
        void OnChanged() => Application.Invoke(() => { if (!closed) Refresh(); });
        service.Changed += OnChanged;

        checkButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            statusLabel.Text = "Checking for updates…";
            dialog.SetNeedsDraw();
            // Fire-and-forget: OnChanged refreshes the dialog when it completes. Forced check ignores gating.
            _ = service.CheckAsync(force: true);
        };

        updateButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            if (!service.CanUpdateNow)
                return;
            // Record the request and close the dialog. The caller (DoUpdates) sees the pending request
            // and quits the TUI; the app then runs the upgrade in the foreground (see Program).
            service.RequestUpdateNow();
            Application.RequestStop(dialog);
        };

        closeButton.Accepting += (_, e) => { e.Handled = true; Application.RequestStop(dialog); };

        dialog.KeyDown += (_, key) =>
        {
            var code = key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask);
            if (code == KeyCode.Esc)
            {
                key.Handled = true;
                Application.RequestStop(dialog);
            }
        };

        Refresh();
        checkButton.SetFocus();

        try { Application.Run(dialog); }
        finally
        {
            closed = true;
            service.Changed -= OnChanged;
            dialog.Dispose();
        }
    }

    private static string StatusText(UpdateCheckResult? result) => result?.Status switch
    {
        null => "No update check has run yet. Choose \"Check now\" to check.",
        UpdateCheckStatus.UpdateAvailable =>
            $"A newer version v{result.LatestVersion} is available (you have v{result.CurrentVersion}).",
        UpdateCheckStatus.UpToDate => $"You're on the latest version (v{result.CurrentVersion}).",
        UpdateCheckStatus.NotManaged =>
            "This build isn't installed via Homebrew or Scoop, so there's no update check.",
        UpdateCheckStatus.VersionUnknown => "Development build (version unknown); update check skipped.",
        UpdateCheckStatus.Error => $"Update check failed: {result.Error}",
        _ => "",
    };
}
