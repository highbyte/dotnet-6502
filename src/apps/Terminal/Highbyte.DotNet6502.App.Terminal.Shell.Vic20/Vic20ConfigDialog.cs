using System.Collections.ObjectModel;
using System.Diagnostics;
using Highbyte.DotNet6502.App.Terminal;
using Highbyte.DotNet6502.Impl.Terminal.Vic20;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

// Static Application API (Run for the modal dialog) is obsolete in Terminal.Gui 2.4.5 but functional.
#pragma warning disable CS0618

namespace Highbyte.DotNet6502.App.Terminal.Shell.Vic20;

/// <summary>
/// Modal VIC-20 configuration dialog for the terminal host, opened from the VIC-20 menu's "Config"
/// button. Lets the user auto-download or pick the ROM directory and the kernal/basic/chargen ROM
/// files (with file pickers), shows live validation errors, and on OK applies the edited config to the
/// host. A trimmed version of the C64 dialog (the VIC-20 has no SwiftLink or joystick settings here).
/// </summary>
internal static class Vic20ConfigDialog
{
    public static void Show(TuiHostApp host, ILogger logger)
    {
        // Config changes only take effect on (re)build, so only allow editing while stopped.
        if (host.EmulatorState != EmulatorState.Uninitialized)
        {
            logger.LogInformation("Stop the VIC-20 before changing its configuration.");
            return;
        }

        // Edit a clone; only commit to the host on OK.
        var hostConfig = (Vic20TerminalHostConfig)host.CurrentHostSystemConfig.Clone();
        var cfg = hostConfig.SystemConfig;

        var dialog = new Dialog
        {
            Title = "VIC-20 Config  (Esc to cancel)",
            Width = 84,
            Height = 16,
        };
        host.ApplyUiScheme(dialog);

        // ROM download row: automatic download + manual download link.
        var autoDownloadButton = new Button { X = 1, Y = 0, Text = "Auto download ROMs", ShadowStyle = ShadowStyles.None };
        var manualDownloadButton = new Button { X = Pos.Right(autoDownloadButton) + 1, Y = 0, Text = "Manual download link", ShadowStyle = ShadowStyles.None };
        var downloadStatusLabel = new Label { X = 1, Y = 1, Width = Dim.Fill(2), Text = "" };
        dialog.Add(autoDownloadButton, manualDownloadButton, downloadStatusLabel);

        dialog.Add(new Label { X = 1, Y = 2, Text = "ROM files:" });
        var dirField = AddRow(host, dialog, 3, "ROM dir:", cfg.ROMDirectory, isDirectory: true, cfg);
        var kernalField = AddRow(host, dialog, 4, "Kernal:", RomFile(cfg, Vic20SystemConfig.KERNAL_ROM_NAME), isDirectory: false, cfg);
        var basicField = AddRow(host, dialog, 5, "Basic:", RomFile(cfg, Vic20SystemConfig.BASIC_ROM_NAME), isDirectory: false, cfg);
        var chargenField = AddRow(host, dialog, 6, "Chargen:", RomFile(cfg, Vic20SystemConfig.CHARGEN_ROM_NAME), isDirectory: false, cfg);
        var effectiveRomDirectoryLabel = new Label { X = 1, Y = 7, Width = Dim.Fill(2), Text = "" };
        dialog.Add(effectiveRomDirectoryLabel);

        var validationLabel = new Label { X = 1, Y = 9, Text = "Validation errors:" };
        var validationList = new ListView { X = 1, Y = 10, Width = Dim.Fill(2), Height = 3 };
        validationList.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        dialog.Add(validationLabel, validationList);

        var okButton = new Button { Text = "OK", IsDefault = true, ShadowStyle = ShadowStyles.None };
        var cancelButton = new Button { Text = "Cancel", ShadowStyle = ShadowStyles.None };

        void Sync()
        {
            cfg.ROMDirectory = dirField.Text;
            cfg.SetROM(Vic20SystemConfig.KERNAL_ROM_NAME, kernalField.Text);
            cfg.SetROM(Vic20SystemConfig.BASIC_ROM_NAME, basicField.Text);
            cfg.SetROM(Vic20SystemConfig.CHARGEN_ROM_NAME, chargenField.Text);
            effectiveRomDirectoryLabel.Text = $"Effective: {PathHelper.ExpandOSEnvironmentVariables(cfg.EffectiveROMDirectory)}";
        }

        void Validate()
        {
            Sync();
            var isValid = hostConfig.IsValid(out var errors);
            validationList.SetSource(new ObservableCollection<string>(
                isValid ? new List<string> { "(none)" } : errors));
            validationLabel.Visible = !isValid;
            validationList.Visible = !isValid;
            okButton.Enabled = isValid;
            dialog.SetNeedsDraw();
        }

        dirField.TextChanged += (_, _) => Validate();
        kernalField.TextChanged += (_, _) => Validate();
        basicField.TextChanged += (_, _) => Validate();
        chargenField.TextChanged += (_, _) => Validate();

        // Auto-download: fetch each ROM into the configured directory, then reflect the downloaded
        // filenames in the fields and re-validate. UI updates after the await are marshalled back to
        // the UI thread via Application.Invoke.
        autoDownloadButton.Accepting += async (_, e) =>
        {
            e.Handled = true;
            Sync();
            downloadStatusLabel.Text = "Downloading ROMs…";
            dialog.SetNeedsDraw();

            string status;
            try
            {
                await DownloadRoms(cfg);
                status = "ROMs downloaded OK";
            }
            catch (Exception ex)
            {
                logger.LogError("ROM download failed: {Message}", ex.Message);
                status = ex.Message;
            }

            Application.Invoke(() =>
            {
                kernalField.Text = RomFile(cfg, Vic20SystemConfig.KERNAL_ROM_NAME);
                basicField.Text = RomFile(cfg, Vic20SystemConfig.BASIC_ROM_NAME);
                chargenField.Text = RomFile(cfg, Vic20SystemConfig.CHARGEN_ROM_NAME);
                downloadStatusLabel.Text = status;
                Validate();
            });
        };

        manualDownloadButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            try
            {
                var url = new Uri(cfg.ROMDownloadUrls[Vic20SystemConfig.KERNAL_ROM_NAME]).GetLeftPart(UriPartial.Authority);
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                downloadStatusLabel.Text = $"Opened {url} in browser.";
            }
            catch (Exception ex)
            {
                logger.LogError("Could not open ROM download link: {Message}", ex.Message);
                downloadStatusLabel.Text = $"Could not open browser: {ex.Message}";
            }
            dialog.SetNeedsDraw();
        };

        okButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            Sync();
            if (!hostConfig.IsValid(out List<string> _))
            {
                Validate();
                return;
            }
            host.UpdateHostSystemConfig(hostConfig);
            logger.LogInformation("VIC-20 configuration updated.");
            Application.RequestStop(dialog);
        };
        cancelButton.Accepting += (_, e) => { e.Handled = true; Application.RequestStop(dialog); };

        dialog.KeyDown += (_, key) =>
        {
            if ((key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask)) == KeyCode.Esc)
            {
                key.Handled = true;
                Application.RequestStop(dialog);
            }
        };

        // Cancel on the left, OK on the right — matching the other host apps' dialogs.
        dialog.AddButton(cancelButton);
        dialog.AddButton(okButton);

        Validate();

        try { Application.Run(dialog); }
        finally { dialog.Dispose(); }
    }

    private static string RomFile(Vic20SystemConfig cfg, string romName)
        => cfg.GetROM(romName).File ?? string.Empty;

    /// <summary>
    /// Download each ROM in <see cref="Vic20SystemConfig.ROMDownloadUrls"/> into the configured ROM
    /// directory (created if needed) and point the config's ROM entries at the downloaded files.
    /// </summary>
    private static async Task DownloadRoms(Vic20SystemConfig cfg)
    {
        var romFolder = PathHelper.ExpandOSEnvironmentVariables(cfg.EffectiveROMDirectory);
        if (!Directory.Exists(romFolder))
            Directory.CreateDirectory(romFolder);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");

        foreach (var (romName, romUrl) in cfg.ROMDownloadUrls)
        {
            var filename = Path.GetFileName(new Uri(romUrl).LocalPath);
            var dest = Path.Combine(romFolder, filename);
            try
            {
                using var response = await httpClient.GetAsync(romUrl);
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to get '{romUrl}' ({(int)response.StatusCode})");
                await using (var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                    await response.Content.CopyToAsync(fs);

                cfg.SetROM(romName, filename);
            }
            catch (Exception ex)
            {
                if (File.Exists(dest))
                    File.Delete(dest);
                throw new Exception($"Error downloading {romUrl}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Add a "label / text field / [...]" row. The "..." button opens a file (or directory) picker
    /// rooted at the configured ROM directory and writes the chosen value back into the text field.
    /// </summary>
    private static TextField AddRow(
        TuiHostApp host, Dialog dialog, int y, string label, string value, bool isDirectory,
        Vic20SystemConfig cfg)
    {
        dialog.Add(new Label { X = 1, Y = y, Text = label });

        var field = new TextField { X = 11, Y = y, Width = Dim.Fill(10), Text = value };
        var pickButton = new Button { X = Pos.AnchorEnd(8), Y = y, Text = "...", ShadowStyle = ShadowStyles.None };
        pickButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            var picked = PickPath(host, cfg, isDirectory);
            if (picked == null)
                return;
            field.Text = isDirectory ? picked : Path.GetFileName(picked);
        };

        dialog.Add(field, pickButton);
        return field;
    }

    private static string? PickPath(TuiHostApp host, Vic20SystemConfig cfg, bool isDirectory)
    {
        var startDir = PathHelper.ExpandOSEnvironmentVariables(cfg.EffectiveROMDirectory);
        using var picker = new OpenDialog
        {
            Title = isDirectory ? "Select ROM directory" : "Select ROM file",
            OpenMode = isDirectory ? OpenMode.Directory : OpenMode.File,
            AllowsMultipleSelection = false,
        };
        host.ApplyUiScheme(picker);
        if (Directory.Exists(startDir))
            picker.Path = startDir.EndsWith(Path.DirectorySeparatorChar) ? startDir : startDir + Path.DirectorySeparatorChar;

        Application.Run(picker);

        if (picker.Canceled || picker.FilePaths.Count == 0)
            return null;
        return picker.FilePaths[0];
    }
}
