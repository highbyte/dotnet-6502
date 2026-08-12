using Highbyte.DotNet6502.Impl.Terminal.Apple2;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Apple2.Disk2;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextCopy;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

#pragma warning disable CS0618

namespace Highbyte.DotNet6502.App.Terminal.Shell.Apple2;

/// <summary>Apple II copy/paste, Disk II, joystick and configuration controls.</summary>
public sealed class Apple2TerminalMenuView : View, ITerminalMenuContribution
{
    private readonly TuiHostApp _host;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Button _copyButton;
    private readonly Button _pasteButton;
    private readonly Button _diskButton;
    private readonly Button _bootButton;
    private readonly CheckBox _joystickCheck;
    private readonly Button _configButton;

    public string MenuTitle => "Apple II";

    public int MenuRowCount => 4;

    public View View => this;

    public Apple2TerminalMenuView(TuiHostApp host, ILoggerFactory loggerFactory)
    {
        _host = host;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(nameof(Apple2TerminalMenuView));

        _copyButton = new Button { X = 0, Y = 0, Text = "Copy", ShadowStyle = ShadowStyles.None };
        _copyButton.Accepting += (_, e) => { e.Handled = true; CopyBasicSource(); };

        _pasteButton = new Button { X = 12, Y = 0, Text = "Paste", ShadowStyle = ShadowStyles.None };
        _pasteButton.Accepting += (_, e) => { e.Handled = true; PasteText(); };

        _diskButton = new Button { X = 0, Y = 1, Text = "Insert .dsk", ShadowStyle = ShadowStyles.None };
        _diskButton.Accepting += async (_, e) => { e.Handled = true; await ToggleDisk(); };

        _bootButton = new Button { X = 15, Y = 1, Text = "Boot", ShadowStyle = ShadowStyles.None };
        _bootButton.Accepting += async (_, e) => { e.Handled = true; await BootDisk(); };

        _joystickCheck = new CheckBox
        {
            X = 0,
            Y = 2,
            Text = "WASD joystick",
            Value = CurrentConfig?.KeyboardJoystickEnabled == true
                ? CheckState.Checked
                : CheckState.UnChecked,
        };
        _joystickCheck.ValueChanged += (_, e) => SetKeyboardJoystick(e.NewValue == CheckState.Checked);

        _configButton = new Button { X = 0, Y = 3, Text = "Config…", ShadowStyle = ShadowStyles.None };
        _configButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            Apple2ConfigDialog.Show(_host, _loggerFactory);
            SyncControls();
        };

        Add(_copyButton, _pasteButton, _diskButton, _bootButton, _joystickCheck, _configButton);
        SyncControls();
    }

    public void RefreshControlStates()
    {
        var initialized = _host.EmulatorState != EmulatorState.Uninitialized;
        _copyButton.Enabled = _host.EmulatorState == EmulatorState.Running;
        _pasteButton.Enabled = _host.EmulatorState == EmulatorState.Running;
        _diskButton.Enabled = initialized;
        _bootButton.Enabled = initialized
            && _host.CurrentRunningSystem is Apple2System apple2
            && apple2.DiskController.IsDiskInserted;
        _configButton.Enabled = !initialized;
        SyncControls();
    }

    private global::Highbyte.DotNet6502.Systems.Apple2.Config.Apple2SystemConfig? CurrentConfig =>
        (_host.CurrentHostSystemConfig as Apple2TerminalHostConfig)?.SystemConfig;

    private void CopyBasicSource()
    {
        if (_host.CurrentRunningSystem is not Apple2System apple2)
            return;
        try
        {
            ClipboardService.SetText(apple2.BasicTokenParser.GetBasicText());
            _logger.LogInformation("Copied Applesoft BASIC source to clipboard.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not copy Applesoft BASIC source.");
        }
    }

    private void PasteText()
    {
        if (_host.CurrentRunningSystem is not Apple2System apple2)
            return;
        try
        {
            var text = ClipboardService.GetText();
            if (!string.IsNullOrEmpty(text))
                apple2.TextPaste.Paste(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not paste text into the Apple II.");
        }
    }

    private async Task ToggleDisk()
    {
        if (_host.CurrentRunningSystem is not Apple2System apple2)
            return;

        if (apple2.DiskController.IsDiskInserted)
        {
            Apple2DiskBoot.Eject(_host, _logger);
            SyncControls();
            return;
        }

        try
        {
            using var dialog = new OpenDialog
            {
                Title = "Insert Apple II .dsk image",
                OpenMode = OpenMode.File,
                AllowsMultipleSelection = false,
            };
            _host.ApplyUiScheme(dialog);
            Application.Run(dialog);
            if (dialog.Canceled || dialog.FilePaths.Count == 0)
                return;

            var path = dialog.FilePaths[0];
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            await Apple2DiskBoot.InsertAsync(_host, await File.ReadAllBytesAsync(path), _logger);
            _logger.LogInformation("Inserted {Disk}; disk is write-protected.", Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not insert Apple II disk image.");
        }
        SyncControls();
    }

    private async Task BootDisk()
    {
        try
        {
            await Apple2DiskBoot.BootAsync(_host, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not boot the Apple II disk image.");
        }
        SyncControls();
    }

    private void SetKeyboardJoystick(bool enabled)
    {
        if (_host.CurrentHostSystemConfig is not Apple2TerminalHostConfig hostConfig)
            return;

        hostConfig.SystemConfig.KeyboardJoystickEnabled = enabled;
        if (_host.CurrentRunningSystem?.InputConsumer is global::Highbyte.DotNet6502.Systems.Apple2.Input.Apple2InputHandler input)
            input.InputConfig.KeyboardJoystickEnabled = enabled;
        else
            _host.UpdateHostSystemConfig(hostConfig);

        _logger.LogInformation("Apple II keyboard joystick {State}.", enabled ? "enabled" : "disabled");
    }

    private void SyncControls()
    {
        var hasDisk = _host.CurrentRunningSystem is Apple2System apple2
            && apple2.DiskController.IsDiskInserted;
        _diskButton.Text = hasDisk ? "Eject disk" : "Insert .dsk";
        _bootButton.Enabled = hasDisk;
        if (CurrentConfig is { } config)
        {
            _joystickCheck.Value = config.KeyboardJoystickEnabled
                ? CheckState.Checked
                : CheckState.UnChecked;
        }
    }
}
