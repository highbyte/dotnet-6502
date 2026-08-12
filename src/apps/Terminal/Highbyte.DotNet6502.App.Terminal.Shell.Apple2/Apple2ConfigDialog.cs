using System.Collections.ObjectModel;
using System.Diagnostics;
using Highbyte.DotNet6502.Impl.Terminal.Apple2;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Caching;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618

namespace Highbyte.DotNet6502.App.Terminal.Shell.Apple2;

/// <summary>Apple II ROM, display and memory configuration for the Terminal host.</summary>
internal static class Apple2ConfigDialog
{
    public static void Show(TuiHostApp host, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(Apple2ConfigDialog));
        if (host.EmulatorState != EmulatorState.Uninitialized)
        {
            logger.LogInformation("Stop the Apple II before changing its configuration.");
            return;
        }

        var hostConfig = (Apple2TerminalHostConfig)host.CurrentHostSystemConfig.Clone();
        var cfg = hostConfig.SystemConfig;

        var dialog = new Dialog
        {
            Title = "Apple II Config  (Esc to cancel)",
            Width = 88,
            Height = 19,
        };
        host.ApplyUiScheme(dialog);

        var autoDownloadButton = new Button { X = 1, Y = 0, Text = "Auto download ROMs", ShadowStyle = ShadowStyles.None };
        var manualDownloadButton = new Button { X = Pos.Right(autoDownloadButton) + 1, Y = 0, Text = "ROM archive", ShadowStyle = ShadowStyles.None };
        var downloadStatusLabel = new Label { X = 1, Y = 1, Width = Dim.Fill(2), Text = "" };
        dialog.Add(autoDownloadButton, manualDownloadButton, downloadStatusLabel);

        dialog.Add(new Label { X = 1, Y = 2, Text = "ROM files:" });
        var dirField = AddRow(host, dialog, 3, "ROM dir:", cfg.ROMDirectory, true, cfg);
        var systemField = AddRow(host, dialog, 4, "System:", RomFile(cfg, Apple2SystemConfig.SYSTEM_ROM_NAME), false, cfg);
        var chargenField = AddRow(host, dialog, 5, "Chargen:", RomFile(cfg, Apple2SystemConfig.CHARGEN_ROM_NAME), false, cfg);
        var disk2Field = AddRow(host, dialog, 6, "Disk II:", RomFile(cfg, Apple2SystemConfig.DISK2_ROM_NAME), false, cfg);
        var effectiveRomDirectoryLabel = new Label { X = 1, Y = 7, Width = Dim.Fill(2), Text = "" };
        dialog.Add(effectiveRomDirectoryLabel);

        var monitorValues = Enum.GetNames<Apple2MonitorColor>();
        dialog.Add(new Label { X = 1, Y = 9, Text = "Monitor:" });
        var monitorDropDown = new DropDownList
        {
            X = 11,
            Y = 9,
            Width = 18,
            Source = new ListWrapper<string>(new ObservableCollection<string>(monitorValues)),
            ReadOnly = true,
            Text = cfg.MonitorColor.ToString(),
        };
        var languageCardCheck = new CheckBox
        {
            X = 34,
            Y = 9,
            Text = "64 KB language card",
            Value = cfg.LanguageCardEnabled ? CheckState.Checked : CheckState.UnChecked,
        };
        dialog.Add(monitorDropDown, languageCardCheck);

        var validationLabel = new Label { X = 1, Y = 11, Text = "Validation errors:" };
        var validationList = new ListView { X = 1, Y = 12, Width = Dim.Fill(2), Height = 3 };
        validationList.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        dialog.Add(validationLabel, validationList);

        var okButton = new Button { Text = "OK", IsDefault = true, ShadowStyle = ShadowStyles.None };
        var cancelButton = new Button { Text = "Cancel", ShadowStyle = ShadowStyles.None };

        void Sync()
        {
            cfg.ROMDirectory = dirField.Text;
            cfg.SetROM(Apple2SystemConfig.SYSTEM_ROM_NAME, EmptyToNull(systemField.Text));
            cfg.SetROM(Apple2SystemConfig.CHARGEN_ROM_NAME, EmptyToNull(chargenField.Text));
            if (string.IsNullOrWhiteSpace(disk2Field.Text))
                cfg.ROMs.RemoveAll(rom => rom.Name == Apple2SystemConfig.DISK2_ROM_NAME);
            else
                cfg.SetROM(Apple2SystemConfig.DISK2_ROM_NAME, disk2Field.Text);

            if (Enum.TryParse<Apple2MonitorColor>(monitorDropDown.Text.ToString(), out var monitorColor))
                cfg.MonitorColor = monitorColor;
            cfg.LanguageCardEnabled = languageCardCheck.Value == CheckState.Checked;
            cfg.AudioEnabled = false;
            effectiveRomDirectoryLabel.Text =
                $"Effective: {PathHelper.ExpandOSEnvironmentVariables(cfg.EffectiveROMDirectory)}";
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
        systemField.TextChanged += (_, _) => Validate();
        chargenField.TextChanged += (_, _) => Validate();
        disk2Field.TextChanged += (_, _) => Validate();
        languageCardCheck.ValueChanged += (_, _) => Validate();

        autoDownloadButton.Accepting += async (_, e) =>
        {
            e.Handled = true;
            Sync();
            downloadStatusLabel.Text = "Downloading ROMs…";
            dialog.SetNeedsDraw();

            string status;
            try
            {
                var cache = new FileDownloadCache(AppStoragePaths.GetDownloadCacheDirectory(), loggerFactory);
                using var httpClient = new HttpClient();
                var downloader = new RomDownloader(loggerFactory, httpClient, downloadCache: cache);
                var files = await downloader.DownloadRomsToFilesAsync(
                    cfg.ROMDownloadSources,
                    cfg.EffectiveROMDirectory);
                foreach (var (romName, fileName) in files)
                    cfg.SetROM(romName, fileName);
                status = "ROMs downloaded OK";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Apple II ROM download failed.");
                status = ex.Message;
            }

            Application.Invoke(() =>
            {
                systemField.Text = RomFile(cfg, Apple2SystemConfig.SYSTEM_ROM_NAME);
                chargenField.Text = RomFile(cfg, Apple2SystemConfig.CHARGEN_ROM_NAME);
                disk2Field.Text = RomFile(cfg, Apple2SystemConfig.DISK2_ROM_NAME);
                downloadStatusLabel.Text = status;
                Validate();
            });
        };

        manualDownloadButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            try
            {
                Process.Start(new ProcessStartInfo(Apple2SystemConfig.ROM_SOURCE_INFO_URL) { UseShellExecute = true });
                downloadStatusLabel.Text = "Opened the Apple II ROM archive.";
            }
            catch (Exception ex)
            {
                downloadStatusLabel.Text = $"Could not open browser: {ex.Message}";
            }
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
            logger.LogInformation("Apple II configuration updated.");
            Application.RequestStop(dialog);
        };
        cancelButton.Accepting += (_, e) => { e.Handled = true; Application.RequestStop(dialog); };

        dialog.KeyDown += (_, key) =>
        {
            if ((key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask)) != KeyCode.Esc)
                return;
            key.Handled = true;
            Application.RequestStop(dialog);
        };

        dialog.AddButton(cancelButton);
        dialog.AddButton(okButton);
        Validate();

        try { Application.Run(dialog); }
        finally { dialog.Dispose(); }
    }

    private static string RomFile(Apple2SystemConfig cfg, string romName)
        => cfg.HasROM(romName) ? cfg.GetROM(romName).File ?? string.Empty : string.Empty;

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static TextField AddRow(
        TuiHostApp host,
        Dialog dialog,
        int y,
        string label,
        string value,
        bool isDirectory,
        Apple2SystemConfig cfg)
    {
        dialog.Add(new Label { X = 1, Y = y, Text = label });
        var field = new TextField { X = 11, Y = y, Width = Dim.Fill(10), Text = value };
        var pickButton = new Button { X = Pos.AnchorEnd(8), Y = y, Text = "...", ShadowStyle = ShadowStyles.None };
        pickButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            var picked = PickPath(host, cfg, isDirectory);
            if (picked != null)
                field.Text = isDirectory ? picked : Path.GetFileName(picked);
        };
        dialog.Add(field, pickButton);
        return field;
    }

    private static string? PickPath(TuiHostApp host, Apple2SystemConfig cfg, bool isDirectory)
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
        {
            picker.Path = startDir.EndsWith(Path.DirectorySeparatorChar)
                ? startDir
                : startDir + Path.DirectorySeparatorChar;
        }
        Application.Run(picker);
        return picker.Canceled || picker.FilePaths.Count == 0 ? null : picker.FilePaths[0];
    }
}
