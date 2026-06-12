using System.Collections.ObjectModel;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Utils;
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
/// Modal machine-code monitor for the terminal host, opened with F12 (or the Monitor button) while a
/// system is running or paused. Shows the accumulated monitor output in a scrollable list, the current
/// CPU/system status, and a command input line. Commands are the same as the other host apps' monitor
/// (type <c>?</c> for help). Closes on Esc / F12, or when a command continues execution (<c>g</c>) or
/// quits the monitor (<c>x</c>).
/// </summary>
internal static class MonitorDialog
{
    /// <summary>
    /// Runs the monitor dialog modally against <paramref name="monitor"/>. Returns the last command
    /// result so the caller can decide whether to resume the emulator (Continue) or leave it as-is.
    /// </summary>
    public static CommandResult Show(TuiHostApp host, TuiMonitor monitor)
    {
        var lastResult = CommandResult.Ok;

        // Full-screen modal. A Dialog can't be used: it forces a centered layout with a built-in
        // margin, so it never reaches the screen edges (a sliver of the main window bleeds through on
        // the right and bottom). A plain bordered Window (Toplevel) fills the whole screen instead.
        var dialog = new Window
        {
            Title = "Monitor  (Esc / F12 to close,  ? for help)",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.Single,
        };
        host.ApplyUiScheme(dialog);

        // Output (most space): one row per output line, scrollable, newest at the bottom.
        var outputList = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(3) };
        outputList.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        outputList.HorizontalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;

        // CPU / system status line, refreshed after each command.
        var statusLabel = new Label { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill(), Text = "" };

        // Command input line.
        var promptLabel = new Label { X = 0, Y = Pos.AnchorEnd(1), Text = "> " };
        var inputField = new TextField { X = Pos.Right(promptLabel), Y = Pos.AnchorEnd(1), Width = Dim.Fill() };

        dialog.Add(outputList, statusLabel, promptLabel, inputField);

        void RefreshOutput()
        {
            var rows = monitor.Output.Select(o => o.Message).ToList();
            outputList.SetSource(new ObservableCollection<string>(rows));
            if (rows.Count > 0)
            {
                // Keep the newest output in view.
                outputList.SelectedItem = rows.Count - 1;
                outputList.EnsureSelectedItemVisible();
            }
        }

        void RefreshStatus()
        {
            statusLabel.Text = OutputGen.GetProcessorState(monitor.Cpu, includeCycles: true);
        }

        RefreshOutput();
        RefreshStatus();

        // Enter on the input line: run the command, refresh, and close on continue/quit.
        inputField.Accepting += (_, e) =>
        {
            e.Handled = true;
            var command = inputField.Text?.Trim() ?? string.Empty;
            inputField.Text = string.Empty;
            if (command.Length == 0)
                return;

            monitor.WriteOutput($"> {command}");
            lastResult = monitor.SendCommand(command);

            RefreshOutput();
            RefreshStatus();
            dialog.SetNeedsDraw();

            if (lastResult is CommandResult.Continue or CommandResult.Quit)
                Application.RequestStop(dialog);
        };

        dialog.KeyDown += (_, key) =>
        {
            var code = key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask);
            if (code is KeyCode.Esc or KeyCode.F12)
            {
                key.Handled = true;
                Application.RequestStop(dialog);
            }
        };

        inputField.SetFocus();

        try { Application.Run(dialog); }
        finally { dialog.Dispose(); }

        return lastResult;
    }
}
