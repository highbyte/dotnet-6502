using Highbyte.DotNet6502.Impl.Terminal.Oric;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextCopy;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

#pragma warning disable CS0618

namespace Highbyte.DotNet6502.App.Terminal.Shell.Oric;

/// <summary>Oric BASIC clipboard, tape transport, joystick and configuration controls.</summary>
public sealed class OricTerminalMenuView : View, ITerminalMenuContribution
{
    private readonly TuiHostApp _host;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Button _copyButton;
    private readonly Button _pasteButton;
    private readonly Button _attachButton;
    private readonly Button _ejectButton;
    private readonly Button _rewindButton;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly CheckBox _joystickCheck;
    private readonly Button _configButton;

    public OricTerminalMenuView(TuiHostApp host, ILoggerFactory loggerFactory)
    {
        _host = host;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(nameof(OricTerminalMenuView));

        _copyButton = MakeButton(0, 0, "Copy", CopyBasicSource);
        _pasteButton = MakeButton(12, 0, "Paste", PasteText);
        _attachButton = MakeButton(0, 1, "Attach .tap", AttachTape);
        _ejectButton = MakeButton(15, 1, "Eject", () => RunTapeOperation(oric => oric.EjectTape()));
        _rewindButton = MakeButton(0, 2, "Rewind", () => RunTapeOperation(oric => oric.RewindTape()));
        _previousButton = MakeButton(10, 2, "Prev", () => RunTapeOperation(oric => oric.SeekToPreviousTapeRecord()));
        _nextButton = MakeButton(19, 2, "Next", () => RunTapeOperation(oric => oric.SeekToNextTapeRecord()));

        _joystickCheck = new CheckBox
        {
            X = 0,
            Y = 3,
            Text = "WASD joystick",
            Value = CurrentConfig?.KeyboardJoystickEnabled == true
                ? CheckState.Checked
                : CheckState.UnChecked,
        };
        _joystickCheck.ValueChanged += (_, e) => SetKeyboardJoystick(e.NewValue == CheckState.Checked);

        _configButton = MakeButton(0, 4, "Config…", () =>
        {
            OricConfigDialog.Show(_host, _loggerFactory);
            SyncControls();
        });

        Add(
            _copyButton,
            _pasteButton,
            _attachButton,
            _ejectButton,
            _rewindButton,
            _previousButton,
            _nextButton,
            _joystickCheck,
            _configButton);
        SyncControls();
    }

    public string MenuTitle => "Oric Atmos";

    public int MenuRowCount => 5;

    public View View => this;

    public void RefreshControlStates()
    {
        var initialized = _host.EmulatorState != EmulatorState.Uninitialized;
        var running = _host.EmulatorState == EmulatorState.Running;
        _copyButton.Enabled = running;
        _pasteButton.Enabled = running;
        _attachButton.Enabled = initialized;
        _configButton.Enabled = !initialized;
        SyncControls();
    }

    private global::Highbyte.DotNet6502.Systems.Oric.Config.OricSystemConfig? CurrentConfig =>
        (_host.CurrentHostSystemConfig as OricTerminalHostConfig)?.SystemConfig;

    private Button MakeButton(int x, int y, string text, Action action)
    {
        var button = new Button { X = x, Y = y, Text = text, ShadowStyle = ShadowStyles.None };
        button.Accepting += (_, e) =>
        {
            e.Handled = true;
            action();
        };
        return button;
    }

    private void CopyBasicSource()
    {
        if (_host.CurrentRunningSystem is not OricMachine oric)
            return;
        try
        {
            ClipboardService.SetText(oric.BasicTokenParser.GetBasicText());
            _logger.LogInformation("Copied Oric BASIC source to clipboard.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not copy Oric BASIC source.");
        }
    }

    private void PasteText()
    {
        if (_host.CurrentRunningSystem is not OricMachine oric)
            return;
        try
        {
            var text = ClipboardService.GetText();
            if (!string.IsNullOrEmpty(text))
                oric.TextPaste.Paste(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not paste text into the Oric.");
        }
    }

    private void AttachTape()
    {
        if (_host.CurrentRunningSystem is not OricMachine oric)
            return;

        try
        {
            using var dialog = new OpenDialog
            {
                Title = "Attach Oric .tap image",
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

            var wasRunning = _host.EmulatorState == EmulatorState.Running;
            if (wasRunning)
                _host.Pause();
            try
            {
                var files = oric.InsertTape(File.ReadAllBytes(path), Path.GetFileName(path));
                _logger.LogInformation("Attached {Tape} with {FileCount} file(s).", Path.GetFileName(path), files.Count);
            }
            finally
            {
                if (wasRunning)
                    _host.Start().Wait();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not attach Oric tape image.");
        }
        SyncControls();
    }

    private void RunTapeOperation(Action<OricMachine> operation)
    {
        if (_host.CurrentRunningSystem is not OricMachine oric)
            return;

        var wasRunning = _host.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            _host.Pause();
        try
        {
            operation(oric);
        }
        finally
        {
            if (wasRunning)
                _host.Start().Wait();
        }
        SyncControls();
    }

    private void SetKeyboardJoystick(bool enabled)
    {
        if (_host.CurrentHostSystemConfig is not OricTerminalHostConfig hostConfig)
            return;

        hostConfig.SystemConfig.KeyboardJoystickEnabled = enabled;
        if (_host.CurrentRunningSystem is OricMachine oric)
            oric.Joystick.KeyboardJoystickEnabled = enabled;
        else
            _host.UpdateHostSystemConfig(hostConfig);
        _logger.LogInformation("Oric keyboard joystick {State}.", enabled ? "enabled" : "disabled");
    }

    private void SyncControls()
    {
        var tape = (_host.CurrentRunningSystem as OricMachine)?.Tape;
        var inserted = tape?.IsInserted == true;
        _attachButton.Text = inserted ? "Replace .tap" : "Attach .tap";
        _ejectButton.Enabled = inserted;
        _rewindButton.Enabled = inserted && tape!.Position > 0;
        _previousButton.Enabled = inserted && tape!.CanSeekToPreviousRecord;
        _nextButton.Enabled = inserted && tape!.CanSeekToNextRecord;
        if (CurrentConfig is { } config)
        {
            _joystickCheck.Value = config.KeyboardJoystickEnabled
                ? CheckState.Checked
                : CheckState.UnChecked;
        }
    }
}
