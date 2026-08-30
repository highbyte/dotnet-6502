using System.Collections.ObjectModel;
using System.Diagnostics;
using Highbyte.DotNet6502.Impl.Terminal.Oric;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Caching;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618

namespace Highbyte.DotNet6502.App.Terminal.Shell.Oric;

/// <summary>Oric ROM, keyboard-layout and joystick configuration for the Terminal host.</summary>
internal static class OricConfigDialog
{
    private const string AutoKeyboardLayout = "Auto";

    public static void Show(TuiHostApp host, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(OricConfigDialog));
        if (host.EmulatorState != EmulatorState.Uninitialized)
        {
            logger.LogInformation("Stop the Oric before changing its configuration.");
            return;
        }

        var hostConfig = (OricTerminalHostConfig)host.CurrentHostSystemConfig.Clone();
        var cfg = hostConfig.SystemConfig;
        var input = hostConfig.InputConfig;
        var dialog = new Dialog
        {
            Title = "Oric Atmos Config  (Esc to cancel)",
            Width = 88,
            Height = 21,
        };
        host.ApplyUiScheme(dialog);

        var downloadButton = new Button { X = 1, Y = 0, Text = "Download ROM", ShadowStyle = ShadowStyles.None };
        var sourceButton = new Button { X = Pos.Right(downloadButton) + 1, Y = 0, Text = "ROM information", ShadowStyle = ShadowStyles.None };
        var downloadStatusLabel = new Label { X = 1, Y = 1, Width = Dim.Fill(2), Text = "" };
        dialog.Add(downloadButton, sourceButton, downloadStatusLabel);

        dialog.Add(new Label { X = 1, Y = 3, Text = "Atmos BASIC 1.1b ROM:" });
        var dirField = AddRomRow(host, dialog, 4, "ROM dir:", cfg.ROMDirectory, true, cfg);
        var romField = AddRomRow(host, dialog, 5, "ROM file:", RomFile(cfg), false, cfg);
        var effectiveRomDirectoryLabel = new Label { X = 1, Y = 6, Width = Dim.Fill(2), Text = "" };
        dialog.Add(effectiveRomDirectoryLabel);

        var keyboardLayouts = new[] { AutoKeyboardLayout }.Concat(Enum.GetNames<HostKeyboardLayout>()).ToArray();
        dialog.Add(new Label { X = 1, Y = 8, Text = "Keyboard layout:" });
        var keyboardLayout = MakeDropDown(18, 8, 14, keyboardLayouts,
            input.KeyboardLayout?.ToString() ?? AutoKeyboardLayout);

        dialog.Add(new Label { X = 1, Y = 9, Text = "Joystick interface:" });
        var joystickInterface = MakeDropDown(21, 9, 24, Enum.GetNames<OricJoystickInterface>(),
            cfg.JoystickInterface.ToString());

        var keyboardJoystick = new CheckBox
        {
            X = 1,
            Y = 10,
            Text = "Enable WASD + Space keyboard joystick",
            Value = cfg.KeyboardJoystickEnabled ? CheckState.Checked : CheckState.UnChecked,
        };
        dialog.Add(new Label { X = 1, Y = 11, Text = "Keyboard joystick port:" });
        var keyboardJoystickPort = MakeDropDown(25, 11, 8, ["1", "2"], cfg.KeyboardJoystick.ToString());
        keyboardJoystickPort.Enabled = cfg.KeyboardJoystickEnabled;
        keyboardJoystick.ValueChanged += (_, e) =>
            keyboardJoystickPort.Enabled = e.NewValue == CheckState.Checked;
        dialog.Add(keyboardLayout, joystickInterface, keyboardJoystick, keyboardJoystickPort);

        var validationLabel = new Label { X = 1, Y = 13, Text = "Validation errors:" };
        var validationList = new ListView { X = 1, Y = 14, Width = Dim.Fill(2), Height = 3 };
        validationList.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        dialog.Add(validationLabel, validationList);

        var okButton = new Button { Text = "OK", IsDefault = true, ShadowStyle = ShadowStyles.None };
        var cancelButton = new Button { Text = "Cancel", ShadowStyle = ShadowStyles.None };

        void Sync()
        {
            cfg.ROMDirectory = dirField.Text;
            cfg.SetROM(OricSystemConfig.SystemRomName, EmptyToNull(romField.Text));
            cfg.AudioEnabled = false;
            input.KeyboardLayout = keyboardLayout.Text.ToString() == AutoKeyboardLayout
                ? null
                : Enum.Parse<HostKeyboardLayout>(keyboardLayout.Text.ToString());
            cfg.JoystickInterface = Enum.Parse<OricJoystickInterface>(joystickInterface.Text.ToString());
            cfg.KeyboardJoystickEnabled = keyboardJoystick.Value == CheckState.Checked;
            cfg.KeyboardJoystick = int.Parse(keyboardJoystickPort.Text.ToString());
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
        romField.TextChanged += (_, _) => Validate();
        keyboardJoystick.ValueChanged += (_, _) => Validate();

        downloadButton.Accepting += async (_, e) =>
        {
            e.Handled = true;
            if (!ShowRomLicenseAcknowledgement(host))
            {
                downloadStatusLabel.Text = "ROM download cancelled.";
                return;
            }

            Sync();
            downloadStatusLabel.Text = "Downloading Atmos ROM…";
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
                cfg.SetROM(OricSystemConfig.SystemRomName, files[OricSystemConfig.SystemRomName]);
                status = "Atmos ROM downloaded.";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Oric ROM download failed.");
                status = ex.Message;
            }

            Application.Invoke(() =>
            {
                romField.Text = RomFile(cfg);
                downloadStatusLabel.Text = status;
                Validate();
            });
        };

        sourceButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            try
            {
                Process.Start(new ProcessStartInfo(OricSystemConfig.RomSourceInfoUrl) { UseShellExecute = true });
                downloadStatusLabel.Text = "Opened Oric ROM information.";
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
            logger.LogInformation("Oric Terminal configuration updated.");
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

    private static DropDownList MakeDropDown(
        int x,
        int y,
        int width,
        IEnumerable<string> values,
        string selected)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Source = new ListWrapper<string>(new ObservableCollection<string>(values)),
            ReadOnly = true,
            Text = selected,
        };

    private static bool ShowRomLicenseAcknowledgement(TuiHostApp host)
    {
        var acknowledged = false;
        using var prompt = new Dialog
        {
            Title = "Oric ROM licence acknowledgement",
            Width = 78,
            Height = 12,
        };
        host.ApplyUiScheme(prompt);
        prompt.Add(new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = 5,
            Text = "The Atmos BASIC ROM is copyrighted firmware. The emulator does not grant a licence " +
                "or verify that the download site is authorized to redistribute it.\n\n" +
                "Continue only if you own an Oric Atmos or otherwise have permission to possess and use this ROM.",
        });
        var cancel = new Button { Text = "Cancel", ShadowStyle = ShadowStyles.None };
        var confirm = new Button { Text = "I own/license it — download", IsDefault = true, ShadowStyle = ShadowStyles.None };
        cancel.Accepting += (_, e) => { e.Handled = true; Application.RequestStop(prompt); };
        confirm.Accepting += (_, e) =>
        {
            e.Handled = true;
            acknowledged = true;
            Application.RequestStop(prompt);
        };
        prompt.KeyDown += (_, key) =>
        {
            if ((key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask)) != KeyCode.Esc)
                return;
            key.Handled = true;
            Application.RequestStop(prompt);
        };
        prompt.AddButton(cancel);
        prompt.AddButton(confirm);
        Application.Run(prompt);
        return acknowledged;
    }

    private static TextField AddRomRow(
        TuiHostApp host,
        Dialog dialog,
        int y,
        string label,
        string value,
        bool isDirectory,
        OricSystemConfig cfg)
    {
        dialog.Add(new Label { X = 1, Y = y, Text = label });
        var field = new TextField { X = 12, Y = y, Width = Dim.Fill(10), Text = value };
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

    private static string? PickPath(TuiHostApp host, OricSystemConfig cfg, bool isDirectory)
    {
        var startDir = PathHelper.ExpandOSEnvironmentVariables(cfg.EffectiveROMDirectory);
        using var picker = new OpenDialog
        {
            Title = isDirectory ? "Select ROM directory" : "Select Atmos ROM",
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

    private static string RomFile(OricSystemConfig cfg)
        => cfg.HasROM(OricSystemConfig.SystemRomName)
            ? cfg.GetROM(OricSystemConfig.SystemRomName).File ?? string.Empty
            : string.Empty;

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
